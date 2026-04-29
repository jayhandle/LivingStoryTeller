using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LivingStoryteller
{
    public interface IAIProvider
    {
        Task<string> GetTTSResponse(string content);
        string JSONTTSRequest(string text, string personaDef, string voice, string emotion, string mood);

        Task<string> GetResponse(string content);
        string JSONRequest(string model, string systemPrompt, string userMessage);
    }
}
