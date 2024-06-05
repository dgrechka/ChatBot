using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Billing
{
    public interface IBillingLogger
    {
        Task LogLLMCost(string chatId, string provider, string model, int inputTokenCount, int generatedTokenCount, decimal estimatedCost, string currency, CancellationToken token);
    }
}
