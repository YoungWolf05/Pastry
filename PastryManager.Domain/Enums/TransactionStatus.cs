namespace PastryManager.Domain.Enums;

public enum TransactionStatus
{
    Pending = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Reversed = 5,
    Cancelled = 6
}
