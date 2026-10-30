# Installment package purchases

Create a booking with a purchase-only package item and select `"paymentOption": "installment"`:

```json
{
  "packageId": "<package-id>",
  "customerPackageId": null,
  "quantity": 1,
  "type": "packagePurchase",
  "paymentOption": "installment"
}
```

The package must have an enabled installment plan. The booking response includes
`installmentPurchaseIds`; retries with the same booking idempotency key return the
same IDs. The booking total uses the installment price. Creating the booking does
not confirm collection or release sessions.

Use `GET /api/packages/installment-purchases/{purchaseId}` to read the plan captured
when the package was bought. Confirm each collected amount in order with
`POST /api/packages/installment-purchases/{purchaseId}/installments/{number}/collect`:

```json
{ "amount": 300.00, "paymentMethod": "cash" }
```

Payment number `0` is the first payment. Later numbers match the plan rows. The
amount must exactly match the scheduled amount, and a positive collection needs
an enabled payment method. Repeating a confirmed collection returns its current
state without charging or releasing sessions again. The response shows the
remaining `amountDue` and `availableSessions`.

Only confirmed plan rows release sessions, subject to the prepayment limit. Full
collection releases the full package. A session booking can use the returned
purchase ID once its available balance is positive. The purchase keeps a copy
of its plan, so later catalog edits do not change its payment schedule.

Booking item edits are rejected after an installment purchase; the booking's
payment method can still be edited. Collection records retain the actual method
used for each payment.
