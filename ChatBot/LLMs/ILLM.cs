﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatBot.LLMs
{
    public interface ILLM
    {
        Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken, Chat chat);
    }
}
