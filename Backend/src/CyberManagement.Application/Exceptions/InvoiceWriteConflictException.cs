namespace CyberManagement.Application.Exceptions;

public sealed class InvoiceWriteConflictException()
    : BusinessRuleException("Khóa phát hành hoặc nguồn đã có hóa đơn. Hãy tra lại yêu cầu ban đầu.");
