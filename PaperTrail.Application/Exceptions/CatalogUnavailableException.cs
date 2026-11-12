namespace PaperTrail.Application.Exceptions;

public sealed class CatalogUnavailableException(Exception? innerException = null)
    : Exception("Catalog service is unavailable.", innerException);
