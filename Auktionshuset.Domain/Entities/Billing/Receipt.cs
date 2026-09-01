using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Auktionshuset.Domain.Entities.Customer;

namespace Auktionshuset.Domain.Entities.Billing
{
    public class Receipt
    {
        public required Guid ReceiptId { get; set; }
        public required int ReceiptNumber { get; set; }
        public required DateTime PaymentDateTime { get; set; }
        public required Customer.Customer Customer { get; set; }
        public required Guid CustomerId { get; set; }
    }
}
