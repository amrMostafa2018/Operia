using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Operia.Application.Auth;
using Operia.Controllers;
using Xunit;

namespace Operia.Application.Tests.Authorization;

public sealed class EndpointAuthorizationContractTests
{
    private static readonly IReadOnlyDictionary<string, string?> ExpectedPolicies =
        new Dictionary<string, string?>
        {
            ["AdminController.ApproveAddBalancePlatform"] = Policies.Platform.Manage,
            ["AdminController.ActivateSubscription"] = Policies.Platform.Manage,

            ["AuthController.Register"] = null,
            ["AuthController.VerifyRegisterOtp"] = null,
            ["AuthController.ResendRegisterOtp"] = null,
            ["AuthController.Login"] = null,
            ["AuthController.VerifyOtp"] = null,
            ["AuthController.CompleteFirstLogin"] = null,
            ["AuthController.ResendLoginOtp"] = null,
            ["AuthController.Refresh"] = null,
            ["AuthController.GetCapabilities"] = Policies.AuthenticatedUser,
            ["AuthController.Logout"] = Policies.AuthenticatedUser,
            ["AuthController.ForgotPassword"] = null,
            ["AuthController.VerifyForgotPasswordOtp"] = null,
            ["AuthController.ResetPassword"] = null,
            ["AuthController.ResendForgotPasswordOtp"] = null,

            ["BranchesController.List"] = Policies.BranchesRead,
            ["BranchesController.Get"] = Policies.BranchesRead,
            ["BranchesController.Create"] = Policies.BranchesManage,
            ["BranchesController.Update"] = Policies.BranchesManage,
            ["BranchesController.Delete"] = Policies.BranchesManage,

            ["EmployeesController.List"] = Policies.EmployeesRead,
            ["EmployeesController.Get"] = Policies.EmployeesRead,
            ["EmployeesController.GetSchedule"] = Policies.EmployeesRead,
            ["EmployeesController.UpdateSchedule"] = Policies.EmployeesManage,
            ["EmployeesController.Bookable"] = Policies.BookingsManage,
            ["EmployeesController.Create"] = Policies.EmployeesManage,
            ["EmployeesController.Update"] = Policies.EmployeesManage,
            ["EmployeesController.Role"] = Policies.EmployeesChangeRole,
            ["EmployeesController.Status"] = Policies.EmployeesChangeStatus,

            ["FinanceController.GetSubscriptions"] = Policies.SubscriptionsRead,
            ["FinanceController.ExportSubscriptions"] = Policies.SubscriptionsExport,

            ["PackagesController.List"] = Policies.PackagesRead,
            ["PackagesController.Get"] = Policies.PackagesRead,
            ["PackagesController.ListServiceCategories"] = Policies.PackagesRead,
            ["PackagesController.ListSubServiceCategories"] = Policies.PackagesRead,
            ["PackagesController.Create"] = Policies.PackagesManage,
            ["PackagesController.Update"] = Policies.PackagesManage,
            ["PackagesController.Delete"] = Policies.PackagesManage,
            ["PackagesController.CreateServiceCategory"] = Policies.PackagesManage,
            ["PackagesController.CreateSubServiceCategory"] = Policies.PackagesManage,

            ["OnboardingController.GetStatus"] = Policies.AuthenticatedUser,
            ["OnboardingController.GetPlans"] = null,
            ["OnboardingController.SetupBusiness"] = Policies.OnboardingManage,
            ["OnboardingController.Complete"] = Policies.OnboardingManage,
            ["OnboardingController.AddBalancePlatform"] = Policies.OnboardingManage,
            ["OnboardingController.Activate"] = Policies.OnboardingManage,

            ["SettingsController.GetIdentity"] = Policies.SettingsIdentityRead,
            ["SettingsController.UpdateIdentity"] = Policies.SettingsIdentityManage,
            ["SettingsController.GetPaymentMethods"] = Policies.SettingsPaymentsRead,
            ["SettingsController.UpdatePaymentMethods"] = Policies.SettingsPaymentsManage,
            ["SettingsController.GetWorkingDays"] = Policies.SettingsWorkingDaysRead,
            ["SettingsController.UpdateWorkingDays"] = Policies.SettingsWorkingDaysManage,
            ["SettingsController.GetSecurity"] = Policies.SettingsSecurityRead,
            ["SettingsController.UpdateSecurity"] = Policies.SettingsSecurityManage,
            ["SettingsController.SendPasswordOtp"] = Policies.SettingsPasswordChange,
            ["SettingsController.ChangePassword"] = Policies.SettingsPasswordChange,
            ["SettingsController.BanUser"] = Policies.SettingsUsersBan,
            ["SettingsController.DeleteUser"] = Policies.SettingsUsersDelete,
            ["SettingsController.Deactivate"] = Policies.SettingsAccountDeactivate
        };

    [Fact]
    public void HttpEndpoints_MatchTheApprovedAuthorizationContract()
    {
        var endpoints = typeof(BranchesController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract && type.IsPublic)
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
                .Select(method => new Endpoint(controller, method)))
            .ToArray();

        endpoints.Select(endpoint => endpoint.Key)
            .Should()
            .BeEquivalentTo(ExpectedPolicies.Keys);

        foreach (var endpoint in endpoints)
        {
            var expectedPolicy = ExpectedPolicies[endpoint.Key];
            var allowsAnonymous = endpoint.Method.GetCustomAttributes<AllowAnonymousAttribute>().Any()
                                  || endpoint.Controller.GetCustomAttributes<AllowAnonymousAttribute>().Any();
            var authorization = endpoint.Method.GetCustomAttributes<AuthorizeAttribute>().FirstOrDefault()
                                ?? endpoint.Controller.GetCustomAttributes<AuthorizeAttribute>().FirstOrDefault();

            if (expectedPolicy is null)
            {
                allowsAnonymous.Should().BeTrue($"{endpoint.Key} is an approved public endpoint");
                continue;
            }

            allowsAnonymous.Should().BeFalse($"{endpoint.Key} is protected");
            authorization.Should().NotBeNull($"{endpoint.Key} requires {expectedPolicy}");
            authorization!.Policy.Should().Be(expectedPolicy, $"{endpoint.Key} has an approved policy contract");
        }
    }

    private sealed record Endpoint(Type Controller, MethodInfo Method)
    {
        public string Key => $"{Controller.Name}.{Method.Name}";
    }
}
