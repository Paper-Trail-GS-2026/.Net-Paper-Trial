using PaperTrail.Domain.Exceptions;

namespace PaperTrail.Application.DTOs;

public sealed class PaginationQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public void Validate()
    {
        if (Page < 1 || PageSize is < 1 or > 100 || (long)(Page - 1) * PageSize > int.MaxValue)
            throw new DomainException("Page must be positive and pageSize must be between 1 and 100.");
    }
}
