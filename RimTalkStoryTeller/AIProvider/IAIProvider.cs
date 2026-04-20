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
        Task<string> GetResponse(string content);
        string JSONRequest(string text, string personaDef, string voice, string emotion);
    }
}
