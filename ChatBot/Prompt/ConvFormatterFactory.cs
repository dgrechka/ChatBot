using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.Prompt
{
    public interface IConversationFormatterFactory
    {
        IConversationFormatter GetFormatter();
    }
    public class ConversationFormatterFactoryTransient : IConversationFormatterFactory
    {
        private readonly UserMessageContext _userMessageContext;
        private readonly DateTime _now;

        public ConversationFormatterFactoryTransient(UserMessageContext userMessageContext, DateTime now)
        {
            _userMessageContext = userMessageContext;
            _now = now;
        }

        public IConversationFormatter GetFormatter()
        {
            return _userMessageContext.ActiveModel switch
            {
                TextCompletionModels.Llama3_8B_instruct => new ConvFormatterLlama3(_now),
                TextCompletionModels.Llama3_70B_instruct => new ConvFormatterLlama3(_now),
                TextCompletionModels.Qwen2_72B_instruct => new ConvFormatterQwen2(_now),
                _ => throw new ArgumentException($"Model {_userMessageContext.ActiveModel} is not supported")
            };
        }
    }
}
