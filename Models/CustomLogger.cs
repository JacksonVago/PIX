using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class CustomLogger : ILogger
    {
        readonly string loggerName;
        readonly CustomLoggerProviderConfiguration loggerConfig;
        public CustomLogger(string name, CustomLoggerProviderConfiguration config)
        {
            this.loggerName = name;
            loggerConfig = config;
        }
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }
        public bool IsEnabled(LogLevel logLevel)
        {
            throw new NotImplementedException();
        }
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception exception, Func<TState, Exception, string> formatter)
        {
            string mensagem = string.Format("{0}: {1} - {2}", logLevel.ToString(),
                eventId.Id, formatter(state, exception));
            EscreverTextoNoArquivo(mensagem);
        }
        private void EscreverTextoNoArquivo(string mensagem)
        {
            string str_arquivo = "pix_log.txt";

            if (!Directory.Exists(@".\log\"))
            {
                Directory.CreateDirectory(@".\log\");
            }

            string caminhoArquivoLog = @".\log\" + str_arquivo;

            FileInfo[] ff = new DirectoryInfo(@".\log\").GetFiles();

            if (ff.Length > 0)
            {
                foreach (FileInfo file in ff)
                {
                    if (((file.Length / 1024) / 1024) > 0.05 && file.Name.ToLower() == str_arquivo)
                    {
                        if (File.Exists(@".\log\" + str_arquivo.Replace("txt", "bkp")))
                        {
                            File.Delete(@".\log\" + str_arquivo.Replace("txt", "bkp"));
                        }
                        File.Move(@".\log\" + str_arquivo, @".\log\" + str_arquivo.Replace("txt", "bkp"));
                    }
                }
            }

            try
            {
                using (StreamWriter streamWriter = new StreamWriter(caminhoArquivoLog, true))
                {
                    streamWriter.WriteLine(mensagem);
                    streamWriter.Close();
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.ToString().IndexOf("used by another process.") > -1 ||
                    ex.Message.ToString().IndexOf("Could not find file") > -1 ||
                    ex.Message.ToString().IndexOf("Unable to find the specified file") > -1                    
                    )
                {
                    return;
                }
                
                using (StreamWriter streamWriter = new StreamWriter(caminhoArquivoLog, true))
                {
                    streamWriter.WriteLine(mensagem);
                    streamWriter.Close();
                }

            }
        }
    }
}
