namespace ConstructionFinance.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string entityName, int id)
        : base($"Không tìm thấy {entityName} với Id = {id}") { }
}
