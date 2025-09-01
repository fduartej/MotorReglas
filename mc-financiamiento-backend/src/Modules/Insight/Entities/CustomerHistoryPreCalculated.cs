using System;

namespace Contoso.Modules.Insight.Entities
{
    public class CustomerHistoryPreCalculated
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public string Frequency { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Variable { get; set; } = string.Empty;
        public string VariableValue { get; set; } = string.Empty;
        public string VariableType { get; set; } = string.Empty;
    }
}