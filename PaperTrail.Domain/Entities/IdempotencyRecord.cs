namespace PaperTrail.Domain.Entities;

public sealed class IdempotencyRecord
{
    public const string CreatePreparationOperation = "CREATE_PREPARATION";

    public string OwnerSubject { get; private set; } = string.Empty;
    public string Operation { get; private set; } = CreatePreparationOperation;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string PayloadHash { get; private set; } = string.Empty;
    public int ResponseStatus { get; private set; }
    public string ResponseJson { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private IdempotencyRecord() { }

    public IdempotencyRecord(string ownerSubject, string idempotencyKey, string payloadHash, string responseJson)
    {
        OwnerSubject = ownerSubject;
        IdempotencyKey = idempotencyKey;
        PayloadHash = payloadHash;
        ResponseStatus = 201;
        ResponseJson = responseJson;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
