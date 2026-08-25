using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using PIX.Interface;
using PIX.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace PIX.Repositories
{
    public class PIXItau : IPIX
    {
        private readonly DataRepository _repData;
        private readonly string _strConnect;
        private readonly PIXRepository _repPIX;
        private readonly UsuarioRepository _repUser;
        private readonly IConfiguration _config;
        private readonly string _webhookUrl;
        private readonly string _webhookUrlHomologa;
        private CriptografiaNtv criptNtv = new CriptografiaNtv();
        private Int64 _intExpire;
        private string _dtmInicioToken;
        private Token token = new Token();
        private readonly ILogger _logger;
        //private readonly ConfigEmail _configEmail;
        private readonly string _path;
        private JObject obj_config;

        private readonly string _remet;
        private readonly string _dest;
        private readonly string _cc;
        private readonly string _cco;
        private readonly string _freq;

        public PIXItau(DataRepository repository, IConfiguration config, PIXRepository repository2, UsuarioRepository usuario, ILogger<PIXItau> log)
        {
            _repData = repository;
            _repPIX = repository2;
            _repUser = usuario;
            _strConnect = config.GetConnectionString("DeafultConnectionStrings") + "@DTILGCF06FW";
            _webhookUrl = config.GetSection("webhook")["url"].ToString();
            _webhookUrlHomologa = config.GetSection("webhook")["urlHomologa"].ToString();
            _config = config;
            _logger = log;
            //_configEmail = new ConfigEmail();

            //config.GetSection("DadosEmail").Bind(_configEmail);
            //_path = Path.Combine(".\\ConfigPIX.json");
            //var JSON = System.IO.File.ReadAllText(_path);
            //obj_config = JObject.Parse(JSON);

            //_path = Path.Combine(@".\" + "configEmail.json");
            _path = Path.Combine(@"./" + "configEmail.json");

            var JSON = System.IO.File.ReadAllText(_path);
            var obj_config = JObject.Parse(JSON);

            _remet = obj_config["EmailPIX"]["remet"].ToString();
            _dest = obj_config["EmailPIX"]["dest"].ToString();
            _cc = obj_config["EmailPIX"]["cc"].ToString();
            _cco = obj_config["EmailPIX"]["cco"].ToString();
            _freq = obj_config["EmailPIX"]["cert_freq"].ToString();
        }

        public string ConfirmaDevolucaoPIX(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_token = "";
            string str_msg = "";
            string str_id = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();
            List<Params> filtros = new List<Params>();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        filtros.AddRange(new List<Params>
                        {
                            new Params { nome = "id", valor = "724748", tipo = typeof(Int64).Name },
                            new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name },
                            new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "int_codfil", valor = pedido.Filial.ToString(), tipo = typeof(Int32).Name },
                            new Params { nome = "str_tipoped", valor = pedido.Tipo.ToString(), tipo = typeof(string).Name },
                            new Params { nome = "int_pedido", valor = pedido.numpedido.ToString(), tipo = typeof(Int64).Name },
                            new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name },
                            new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name },
                            new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name },
                            new Params { nome = "situacao", valor = "5", tipo = typeof(Int16).Name },
                            new Params { nome = "DtIni", valor = "2001-01-01", tipo = typeof(DateTime).Name },
                            new Params { nome = "DtFim", valor = "2001-01-01", tipo = typeof(DateTime).Name }
                        });
                        dtt_trans_pix = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Achou pedido (" + pedido.numpedido.ToString() + ")");

                            //Local homologação
                            //var cert = new X509Certificate2(@"D:\Jackson\Clientes\Guaibim\certificados\2026\pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            var cert = new X509Certificate2(@"./pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                            {
                                str_msg = "Certificado PIX Itaú (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";

                                List<envEmail> envMail = new List<envEmail>();
                                envMail.Add(new envEmail
                                {
                                    id = 0,
                                    id_assunto = 0,
                                    str_remet = _remet,
                                    str_dest = _dest,
                                    str_copias = _cc,
                                    str_copias_oc = _cco,
                                    str_corpo = str_msg,
                                    str_html = "N",
                                    str_anexo = "",
                                    dtm_inclusao = DateTime.Now.AddHours(-3),
                                    int_usuario = 696,
                                    str_erro = "",
                                    int_situacao = -1, //0 - Não enviado / 1 - Enviado / 2 - Com erro / -1 - Uma vez ao dia / -2 - Uma vez por semana / -3 - Uma vez por mês
                                    str_assunto = "Aviso de expiração de certificado - PIX"
                                });

                                str_retorno = ManutencaoTabela<envEmail>("I", envMail, "ntv_tbl_envio_emails", conn, null);

                                //str_ret = _repPIX.EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                            }
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Selecionou certificado " + cert.ToString());


                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(cert);

                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                            handler.ServerCertificateCustomValidationCallback +=
                                (httpRequestMessage, cert2, cetChain, policyErrors) =>
                                {
                                    return policyErrors == SslPolicyErrors.None;
                                };

                            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                                var dict = new Dictionary<string, string>();
                                dict.Add("grant_type", "client_credentials");
                                dict.Add("client_id", dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString());
                                dict.Add("client_secret", dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                str_token = respToken.Content.ReadAsStringAsync().Result;
                                _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                                /*
                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                //Dados do token inicio (08/11/2021 17:00:05) validade (3600) tempo decorrido (-88.23:59:54.2342864)
                                if (Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString()) < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                    respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                else
                                {
                                    token = new Token
                                    {
                                        //access_token = GetAppSetting("PixVariables:token"),
                                        access_token = obj_config["PixVariables"]["token"].ToString(),
                                        token_type = "Bearer",
                                        expires_in = Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString())
                                    };
                                    str_token = JsonConvert.SerializeObject(token);
                                }*/

                                if (str_token.Contains("access_token"))
                                {
                                    if (respToken.StatusCode == HttpStatusCode.OK)
                                    {

                                        token = JsonConvert.DeserializeObject<Token>(str_token);
                                        _intExpire = token.expires_in;

                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);

                                        if (token.access_token.Length > 0)
                                        {

                                            for (int i = 0; i < dtt_trans_pix.Rows.Count; i++)
                                            {
                                                // Associar o token aos headers do objeto
                                                // do tipo HttpClient
                                                client.DefaultRequestHeaders.Accept.Clear();
                                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                                //Envia om para o banco
                                                //Produção
                                                HttpResponseMessage response = client.GetAsync(dtt_trans_pix.Rows[0]["str_urlreg_pix"].ToString() + "/pix/" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "/devolucao/" + dtt_trans_pix.Rows[i]["str_id_devol"].ToString()).Result;
                                                //Novo endpoint
                                                //HttpResponseMessage response = client.GetAsync("https://pix-pj.api.itau.com/regulatorio-pix/v2" + "/pix/" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "/devolucao/" + dtt_trans_pix.Rows[i]["str_id_devol"].ToString()).Result;

                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno banco devolucao " + response.ToString());

                                                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                                                {
                                                    str_ret = response.Content.ReadAsStringAsync().Result;
                                                    if (str_ret.Length > 0)
                                                    {
                                                        DevQRCODE_341 reg_ret = JsonConvert.DeserializeObject<DevQRCODE_341>(str_ret);
                                                        DataRow row = null;

                                                        using (SqlTransaction transaction = conn.BeginTransaction())
                                                        {
                                                            try
                                                            {
                                                                dtt_trans_grv = _repData.CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
                                                                row = dtt_trans_grv.NewRow();
                                                                row["id"] = dtt_trans_pix.Rows[i]["id"];
                                                                row["id_chavepix"] = dtt_trans_pix.Rows[i]["id_chavepix"];
                                                                row["str_txid"] = dtt_trans_pix.Rows[i]["str_txid"];
                                                                row["int_expiracao"] = dtt_trans_pix.Rows[i]["int_expiracao"];
                                                                row["int_cpf_dev"] = dtt_trans_pix.Rows[i]["int_cpf_dev"];
                                                                row["int_cnpj_dev"] = dtt_trans_pix.Rows[i]["int_cnpj_dev"];
                                                                row["str_nome_dev"] = dtt_trans_pix.Rows[i]["str_nome_dev"];
                                                                row["dbl_valor_orig"] = dtt_trans_pix.Rows[i]["dbl_valor_orig"];
                                                                row["str_msg_devedor"] = dtt_trans_pix.Rows[i]["str_msg_devedor"];
                                                                row["str_data_cria"] = Convert.ToDateTime(dtt_trans_pix.Rows[i]["str_data_cria"]).ToString("yyyy-MM-dd hh:mm:ss");
                                                                row["int_revisao"] = dtt_trans_pix.Rows[i]["int_revisao"];
                                                                row["str_location"] = dtt_trans_pix.Rows[i]["str_location"];
                                                                row["int_cpf_pag"] = dtt_trans_pix.Rows[i]["int_cpf_pag"];
                                                                row["int_cnpj_pag"] = dtt_trans_pix.Rows[i]["int_cnpj_pag"];
                                                                row["str_nome_pag"] = dtt_trans_pix.Rows[i]["str_nome_pag"];
                                                                row["str_msg_pagador"] = dtt_trans_pix.Rows[i]["str_msg_pagador"];
                                                                row["str_id_devol"] = dtt_trans_pix.Rows[i]["str_id_devol"].ToString();
                                                                row["str_rtrid_devol"] = reg_ret.rtrid;
                                                                row["dbl_valor_devol"] = reg_ret.valor;
                                                                row["dtm_hora_sol_devol"] = reg_ret.horario.solicitacao;
                                                                row["dtm_hora_liq_devol"] = reg_ret.horario.liquidacao;
                                                                row["int_sit_devol"] = (reg_ret.status == "EM_PROCESSAMENTO" ? 5 : (reg_ret.status == "DEVOLVIDO" ? 9 : dtt_trans_pix.Rows[i]["int_sit_devol"]));
                                                                row["str_idfim"] = dtt_trans_pix.Rows[i]["str_idfim"];
                                                                row["int_filial"] = dtt_trans_pix.Rows[i]["int_filial"];
                                                                row["int_tipoped"] = dtt_trans_pix.Rows[i]["int_tipoped"];
                                                                row["int_pedido"] = dtt_trans_pix.Rows[i]["int_pedido"];
                                                                row["int_operador"] = dtt_trans_pix.Rows[i]["int_operador"];
                                                                row["int_caixa"] = dtt_trans_pix.Rows[i]["int_caixa"];
                                                                row["str_emv"] = dtt_trans_pix.Rows[i]["str_emv"];
                                                                row["int_situacao"] = (reg_ret.status == "EM_PROCESSAMENTO" ? 5 : (reg_ret.status == "DEVOLVIDO" ? 9 : dtt_trans_pix.Rows[i]["int_situacao"]));
                                                                row["int_usu_lib"] = DBNull.Value;
                                                                row["int_usu_dev"] = DBNull.Value;

                                                                dtt_trans_grv.Rows.Add(row);

                                                                stbTran = _repData.SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = _repData.ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                transaction.Commit();
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- PIX.Repositories - DevolucaoPIX Erro : " + ex.Message.ToString());
                                                                transaction.Rollback();
                                                                throw ex;
                                                            }
                                                        }

                                                        str_ret = "Alteração da Transação PIX efetuada com sucesso.";
                                                    }
                                                }
                                                else
                                                {

                                                    if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                                                    {
                                                        str_ret = response.Content.ReadAsStringAsync().Result;
                                                        str_retorno = str_ret;

                                                    }
                                                    else
                                                    {
                                                        str_ret = "Código do erro : " + response.StatusCode.ToString() + " Mensagem: " + response.RequestMessage.ToString();
                                                        str_retorno = str_ret;
                                                    }
                                                }
                                            }

                                        }
                                    }
                                }
                                else
                                {
                                    str_ret = "Problemas na geração do Token.";
                                }

                            }
                        }
                        else
                        {
                            str_retorno = "Não existe pedido para ser registrado no banco";
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- PIX.Repositories - DevolucaoPIX Erro 2: " + ex.Message.ToString());
                        conn.Close();
                        throw ex;
                    }

                    conn.Close();
                    return str_retorno;
                }
            }
            else
            {
                str_retorno = "Os campos Filial, Pedido, Tipo são obrogatórios.Chamada fora do padrão";
            }

            return str_retorno;
        }
        public string ConsultaListaPIX()
        {
            int int_pag = 0;
            int int_pagAtual = 0;
            int int_pagTotal = 0;

            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";
            string str_msg = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder str_json_tk = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            DataTable dtt_pix_ativo = new DataTable();
            DataRow row = null;

            HttpResponseMessage respToken = new HttpResponseMessage();
            ListQRCODE_341 reg_ret = new ListQRCODE_341();
            List<Params> filtros = new List<Params>();

            using (SqlConnection conn = new SqlConnection(_strConnect))
            {
                conn.Open();
                try
                {
                    filtros.AddRange(new List<Params>
                    {
                        new Params { nome = "id", valor = "0", tipo = typeof(Int64).Name },
                        new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name },
                        new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name },
                        new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name },
                        new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name },
                        new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name },
                        new Params { nome = "int_codfil", valor = "0", tipo = typeof(Int32).Name },
                        new Params { nome = "str_tipoped", valor = "", tipo = typeof(string).Name },
                        new Params { nome = "int_pedido", valor = "0", tipo = typeof(Int64).Name },
                        new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name },
                        new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name },
                        new Params { nome = "Itens", valor = "2", tipo = typeof(Int16).Name },
                        new Params { nome = "situacao", valor = "1", tipo = typeof(Int16).Name },
                        new Params { nome = "DtIni", valor = "2001-01-01", tipo = typeof(DateTime).Name },
                        new Params { nome = "DtFim", valor = "2001-01-01", tipo = typeof(DateTime).Name }
                    });
                    dtt_trans_pix = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);

                    if (dtt_trans_pix.Rows.Count > 0)
                    {
                        if (filtros.Count > 0)
                        {
                            filtros.Clear();
                        }
                        filtros.AddRange(new List<Params>
                        {
                            new Params { nome = "id", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name },
                            new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "int_codfil", valor = "0", tipo = typeof(Int32).Name },
                            new Params { nome = "str_tipoped", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "int_pedido", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name },
                            new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name },
                            new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name },
                            new Params { nome = "situacao", valor = "1", tipo = typeof(Int16).Name },
                            new Params { nome = "DtIni", valor = dtt_trans_pix.Rows[0]["Dataini"].ToString(), tipo = typeof(DateTime).Name },
                            new Params { nome = "DtFim", valor = dtt_trans_pix.Rows[0]["Datafim"].ToString(), tipo = typeof(DateTime).Name }
                        });
                        dtt_pix_ativo = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);

                        //Local homologação
                        //var cert = new X509Certificate2(@"D:\Jackson\Clientes\Guaibim\certificados\2026\pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                        var cert = new X509Certificate2(@"./pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                        if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                        {
                            str_msg = "Certificado PIX Itaú (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";

                            List<envEmail> envMail = new List<envEmail>();
                            envMail.Add(new envEmail
                            {
                                id = 0,
                                id_assunto = 0,
                                str_remet = _remet,
                                str_dest = _dest,
                                str_copias = _cc,
                                str_copias_oc = _cco,
                                str_corpo = str_msg,
                                str_html = "N",
                                str_anexo = "",
                                dtm_inclusao = DateTime.Now.AddHours(-3),
                                int_usuario = 696,
                                str_erro = "",
                                int_situacao = -1, //0 - Não enviado / 1 - Enviado / 2 - Com erro / -1 - Uma vez ao dia / -2 - Uma vez por semana / -3 - Uma vez por mês
                                str_assunto = "Aviso de expiração de certificado - PIX"
                            });

                            str_retorno = ManutencaoTabela<envEmail>("I", envMail, "ntv_tbl_envio_emails", conn, null);


                            //str_ret = _repPIX.EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                        }
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Selecionou certificado " + cert.ToString());


                        var handler = new HttpClientHandler();
                        handler.ClientCertificates.Add(cert);

                        if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                        {
                            str_msg = "Certificado (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";

                            List<envEmail> envMail = new List<envEmail>();
                            envMail.Add(new envEmail
                            {
                                id = 0,
                                id_assunto = 0,
                                str_remet = _remet,
                                str_dest = _dest,
                                str_copias = _cc,
                                str_copias_oc = _cco,
                                str_corpo = str_msg,
                                str_html = "N",
                                str_anexo = "",
                                dtm_inclusao = DateTime.Now.AddHours(-3),
                                int_usuario = 696,
                                str_erro = "",
                                int_situacao = -1, //0 - Não enviado / 1 - Enviado / 2 - Com erro / -1 - Uma vez ao dia / -2 - Uma vez por semana / -3 - Uma vez por mês
                                str_assunto = "Aviso de expiração de certificado - PIX"
                            });

                            str_retorno = ManutencaoTabela<envEmail>("I", envMail, "ntv_tbl_envio_emails", conn, null);

                            //str_ret = _repPIX.EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                        }

                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                        handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                        handler.ServerCertificateCustomValidationCallback +=
                            (httpRequestMessage, cert2, cetChain, policyErrors) =>
                            {
                                return policyErrors == SslPolicyErrors.None;
                            };

                        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                        using (var client = new HttpClient(handler))
                        {

                            //Busca autorização
                            str_json.Remove(0, str_json.Length);
                            client.DefaultRequestHeaders.Accept.Clear();
                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                            var dict = new Dictionary<string, string>();
                            dict.Add("grant_type", "client_credentials");
                            dict.Add("client_id", dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString());
                            dict.Add("client_secret", dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                            FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                            //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();

                            /*
                            TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                            if (Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString()) < Convert.ToInt64(tempo.TotalSeconds))
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                str_token = respToken.Content.ReadAsStringAsync().Result;
                                _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            }
                            else
                            {
                                token = new Token
                                {
                                    //access_token = GetAppSetting("PixVariables:token"),
                                    access_token = obj_config["PixVariables"]["token"].ToString(),
                                    token_type = "Bearer",
                                    expires_in = Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString())
                                };
                                str_token = JsonConvert.SerializeObject(token);
                            }*/

                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                            respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                            str_token = respToken.Content.ReadAsStringAsync().Result;
                            _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");


                            if (str_token.Contains("access_token"))
                            {
                                if (respToken.StatusCode == HttpStatusCode.OK)
                                {
                                    token = JsonConvert.DeserializeObject<Token>(str_token);
                                    _intExpire = token.expires_in;

                                    //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                    //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                    //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);

                                    if (token.access_token.Length > 0)
                                    {

                                        for (int i = 0; i < dtt_trans_pix.Rows.Count; i++)
                                        {
                                            // Associar o token aos headers do objeto
                                            // do tipo HttpClient
                                            client.DefaultRequestHeaders.Accept.Clear();
                                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                            str_json.Remove(0, str_json.Length);
                                            str_json.Append("?inicio=" + Convert.ToDateTime(dtt_trans_pix.Rows[i]["Dataini"].ToString()).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
                                            str_json.Append("&fim=" + Convert.ToDateTime(dtt_trans_pix.Rows[i]["Datafim"].ToString()).AddHours(3).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
                                            str_json.Append("&status=CONCLUIDA");
                                            str_json.Append("&paginacao.paginaAtual=0");
                                            str_json.Append("&paginacao.itensPorPagina=100");


                                            //Envia om para o banco
                                            HttpResponseMessage response = client.GetAsync(dtt_trans_pix.Rows[i]["str_urlreg_pix"].ToString() + "/cob" + str_json.ToString()).Result;
                                            //HttpResponseMessage response = client.GetAsync("https://pix-pj.api.itau.com/regulatorio-pix/v2" + "/cob" + str_json.ToString()).Result;


                                            if (response.StatusCode == HttpStatusCode.OK)
                                            {
                                                str_ret = response.Content.ReadAsStringAsync().Result;
                                                if (str_ret.Length > 0)
                                                {
                                                    reg_ret = JsonConvert.DeserializeObject<ListQRCODE_341>(str_ret);

                                                    int_pag = 0;
                                                    int_pagAtual = 0;
                                                    int_pagTotal = reg_ret.parametros.paginacao.quantidadeDePaginas;

                                                    for (int_pag = 0; int_pag <= int_pagTotal - 1; int_pag++)
                                                    {
                                                        if (int_pag > 0)
                                                        {
                                                            /*
                                                            TimeSpan tempo1 = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                                            if (Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString()) < Convert.ToInt64(tempo1.TotalSeconds))
                                                            {
                                                                respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                                                str_token = respToken.Content.ReadAsStringAsync().Result;
                                                            }
                                                            */
                                                            respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                                            str_token = respToken.Content.ReadAsStringAsync().Result;
                                                            _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                                                            if (str_token.Contains("access_token"))
                                                            {
                                                                if (respToken.StatusCode == HttpStatusCode.OK)
                                                                {
                                                                    token = JsonConvert.DeserializeObject<Token>(str_token);
                                                                    _intExpire = token.expires_in;

                                                                    //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                                                    //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                                                    //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);

                                                                    if (token.access_token.Length > 0)
                                                                    {

                                                                        // Associar o token aos headers do objeto
                                                                        // do tipo HttpClient
                                                                        client.DefaultRequestHeaders.Accept.Clear();
                                                                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                                                        str_json.Remove(0, str_json.Length);
                                                                        str_json.Append("?inicio=" + Convert.ToDateTime(dtt_trans_pix.Rows[i]["Dataini"].ToString()).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
                                                                        //str_json.Append("&fim=" + Convert.ToDateTime(dtt_trans_pix.Rows[i]["Datafim"].ToString()).AddHours(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
                                                                        str_json.Append("&fim=" + Convert.ToDateTime(dtt_trans_pix.Rows[i]["Datafim"].ToString()).AddHours(3).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
                                                                        str_json.Append("&status=CONCLUIDA");
                                                                        str_json.Append("&paginacao.paginaAtual=" + int_pag.ToString());
                                                                        str_json.Append("&paginacao.itensPorPagina=100");

                                                                        //Envia om para o banco
                                                                        HttpResponseMessage response2 = client.GetAsync(dtt_trans_pix.Rows[0]["str_urlreg_pix"].ToString() + "/cob" + str_json.ToString()).Result;
                                                                        //Novo Caminho
                                                                        //HttpResponseMessage response2 = client.GetAsync("https://pix-pj.api.itau.com/regulatorio-pix/v2" + "/cob" + str_json.ToString()).Result;


                                                                        if (response2.StatusCode == HttpStatusCode.OK)
                                                                        {
                                                                            str_ret = response2.Content.ReadAsStringAsync().Result;
                                                                            if (str_ret.Length > 0)
                                                                            {
                                                                                reg_ret = JsonConvert.DeserializeObject<ListQRCODE_341>(str_ret);
                                                                            }

                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }

                                                        dtt_trans_grv = _repData.CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn);

                                                        if (reg_ret.cobs.Count > 0)
                                                        {
                                                            for (int p = 0; p < reg_ret.cobs.Count; p++)
                                                            {
                                                                if (reg_ret.cobs[p].status != "ATIVA" &&
                                                                    dtt_pix_ativo.Select("str_txid = '" + reg_ret.cobs[p].txid + "'").Count() > 0)
                                                                {
                                                                    using (SqlTransaction transaction = conn.BeginTransaction())
                                                                    {
                                                                        try
                                                                        {
                                                                            dtt_trans_grv.Rows.Clear();
                                                                            row = dtt_trans_grv.NewRow();
                                                                            row["id"] = -1;
                                                                            row["id_chavepix"] = DBNull.Value;
                                                                            row["str_txid"] = reg_ret.cobs[p].txid;
                                                                            row["int_expiracao"] = DBNull.Value;
                                                                            row["int_cpf_dev"] = reg_ret.cobs[p].devedor.cpf;
                                                                            row["int_cnpj_dev"] = reg_ret.cobs[p].devedor.cnpj;
                                                                            row["str_nome_dev"] = reg_ret.cobs[p].devedor.nome;
                                                                            row["dbl_valor_orig"] = reg_ret.cobs[p].valor.original;
                                                                            row["str_msg_devedor"] = reg_ret.cobs[p].solicitacaopagador;
                                                                            row["str_data_cria"] = DBNull.Value;
                                                                            row["int_revisao"] = DBNull.Value;
                                                                            row["str_location"] = reg_ret.cobs[p].location;
                                                                            row["int_cpf_pag"] = reg_ret.cobs[p].pix[0].pagador.cpf;
                                                                            row["int_cnpj_pag"] = reg_ret.cobs[p].pix[0].pagador.cnpj;
                                                                            row["str_nome_pag"] = reg_ret.cobs[p].pix[0].pagador.nome;
                                                                            row["str_msg_pagador"] = reg_ret.cobs[p].pix[0].infoPagador;
                                                                            if (reg_ret.cobs[p].pix[0].devolucoes != null && reg_ret.cobs[p].pix[0].devolucoes.Count > 0)
                                                                            {
                                                                                row["str_id_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].Id;
                                                                                row["str_rtrid_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].rtrId;
                                                                                row["dbl_valor_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].valor;
                                                                                row["dtm_hora_sol_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].horario.solicitacao;
                                                                                row["dtm_hora_liq_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].horario.liquidacao;
                                                                                row["int_sit_devol"] = (reg_ret.cobs[p].pix[0].devolucoes[0].status == "EM_PROCESSAMENTO" ? 0 : (reg_ret.cobs[p].pix[0].devolucoes[0].status == "DEVOLVIDO" ? 1 : 2));
                                                                            }
                                                                            else
                                                                            {
                                                                                row["str_id_devol"] = DBNull.Value;
                                                                                row["str_rtrid_devol"] = DBNull.Value;
                                                                                row["dbl_valor_devol"] = DBNull.Value;
                                                                                row["dtm_hora_sol_devol"] = DBNull.Value;
                                                                                row["dtm_hora_liq_devol"] = reg_ret.cobs[p].pix[0].horario.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                                                                                row["int_sit_devol"] = DBNull.Value;

                                                                            }
                                                                            row["str_idfim"] = reg_ret.cobs[p].pix[0].endToEndId;
                                                                            row["int_filial"] = DBNull.Value;
                                                                            row["int_tipoped"] = DBNull.Value;
                                                                            row["int_pedido"] = DBNull.Value;
                                                                            row["int_operador"] = DBNull.Value;
                                                                            row["int_caixa"] = DBNull.Value;
                                                                            row["str_emv"] = DBNull.Value;
                                                                            row["int_situacao"] = (reg_ret.cobs[p].status == "ATIVA" ? 1 : (reg_ret.cobs[p].status == "CONCLUIDA" ? 2 : (reg_ret.cobs[p].status == "REMOVIDA_PELO_USUARIO_RECEBEDOR" ? 3 : 4)));
                                                                            row["int_usu_lib"] = DBNull.Value;
                                                                            row["int_usu_dev"] = DBNull.Value;

                                                                            dtt_trans_grv.Rows.Add(row);

                                                                            stbTran = _repData.SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                            str_ret = _repData.ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);
                                                                        }
                                                                        catch (Exception ex)
                                                                        {
                                                                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- PIX.Repositories - ConsultaListaPIX erro: " + ex.Message.ToString());
                                                                            transaction.Rollback();
                                                                            throw ex;
                                                                        }
                                                                        transaction.Commit();
                                                                    }
                                                                }
                                                            }

                                                        }
                                                    }
                                                    str_ret = "Alteração da Transação PIX efetuada com sucesso.";
                                                }
                                            }
                                            else
                                            {

                                                str_ret = "Código do erro : " + response.StatusCode.ToString() + " Mensagem: " + response.RequestMessage.ToString();
                                            }
                                        }

                                    }
                                }
                            }
                            else
                            {
                                str_ret = "Problemas na geração do Token.";
                            }

                        }
                    }
                    else
                    {
                        str_retorno = "Não existe pedido para ser registrado no banco";
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- PIX.Repositories - ConsultaListaPIX erro 2:" + ex.Message.ToString());
                    conn.Close();
                    throw ex;
                }
                conn.Close();
                return str_retorno;
            }
        }

        public string ConsultaPIX(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";
            string str_emv = "";
            string str_msg = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();
            List<Params> filtros = new List<Params>();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        filtros.AddRange(new List<Params>
                        {
                            new Params { nome = "id", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name },
                            new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "int_codfil", valor = pedido.Filial.ToString(), tipo = typeof(Int32).Name },
                            new Params { nome = "str_tipoped", valor = pedido.Tipo.ToString(), tipo = typeof(string).Name },
                            new Params { nome = "int_pedido", valor = pedido.numpedido.ToString(), tipo = typeof(Int64).Name },
                            new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name },
                            new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name },
                            new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name },
                            new Params { nome = "situacao", valor = "-1", tipo = typeof(Int16).Name },
                            new Params { nome = "DtIni", valor = "2001-01-01", tipo = typeof(DateTime).Name },
                            new Params { nome = "DtFim", valor = "2001-01-01", tipo = typeof(DateTime).Name }
                        });
                        dtt_trans_pix = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);
                        //dtt_trans_pix = _repPIX.ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Ativa, 0, Convert.ToDateTime("2001/01/01"), Convert.ToDateTime("2001/01/01"), -2, conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            //Local homologação
                            //var cert = new X509Certificate2(@"D:\Jackson\Clientes\Guaibim\certificados\2026\pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            var cert = new X509Certificate2(@"./pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                            {
                                str_msg = "Certificado PIX Itaú (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";

                                List<envEmail> envMail = new List<envEmail>();
                                envMail.Add(new envEmail
                                {
                                    id = 0,
                                    id_assunto = 0,
                                    str_remet = _remet,
                                    str_dest = _dest,
                                    str_copias = _cc,
                                    str_copias_oc = _cco,
                                    str_corpo = str_msg,
                                    str_html = "N",
                                    str_anexo = "",
                                    dtm_inclusao = DateTime.Now.AddHours(-3),
                                    int_usuario = 696,
                                    str_erro = "",
                                    int_situacao = -1, //0 - Não enviado / 1 - Enviado / 2 - Com erro / -1 - Uma vez ao dia / -2 - Uma vez por semana / -3 - Uma vez por mês
                                    str_assunto = "Aviso de expiração de certificado - PIX"
                                });

                                str_retorno = ManutencaoTabela<envEmail>("I", envMail, "ntv_tbl_envio_emails", conn, null);

                                //str_ret = _repPIX.EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                            }
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Selecionou certificado " + cert.ToString());


                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(cert);

                            if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                            {
                                str_msg = "Certificado (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";


                                List<envEmail> envMail = new List<envEmail>();
                                envMail.Add(new envEmail
                                {
                                    id = 0,
                                    id_assunto = 0,
                                    str_remet = _remet,
                                    str_dest = _dest,
                                    str_copias = _cc,
                                    str_copias_oc = _cco,
                                    str_corpo = str_msg,
                                    str_html = "N",
                                    str_anexo = "",
                                    dtm_inclusao = DateTime.Now.AddHours(-3),
                                    int_usuario = 696,
                                    str_erro = "",
                                    int_situacao = -1, //0 - Não enviado / 1 - Enviado / 2 - Com erro / -1 - Uma vez ao dia / -2 - Uma vez por semana / -3 - Uma vez por mês
                                    str_assunto = "Aviso de expiração de certificado - PIX"
                                });

                                str_retorno = ManutencaoTabela<envEmail>("I", envMail, "ntv_tbl_envio_emails", conn, null);

                                //str_ret = _repPIX.EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                            }

                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                            handler.ServerCertificateCustomValidationCallback +=
                                (httpRequestMessage, cert2, cetChain, policyErrors) =>
                                {
                                    return policyErrors == SslPolicyErrors.None;
                                };

                            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                                var dict = new Dictionary<string, string>();
                                dict.Add("grant_type", "client_credentials");
                                dict.Add("client_id", dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString());
                                dict.Add("client_secret", dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();

                                /*
                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                if (Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString()) < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                    respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                else
                                {
                                    token = new Token
                                    {
                                        //access_token = GetAppSetting("PixVariables:token"),
                                        access_token = obj_config["PixVariables"]["token"].ToString(),
                                        token_type = "Bearer",
                                        expires_in = Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString())
                                    };
                                    str_token = JsonConvert.SerializeObject(token);
                                }*/

                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                str_token = respToken.Content.ReadAsStringAsync().Result;
                                _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");


                                if (str_token.Contains("access_token"))
                                {
                                    if (respToken.StatusCode == HttpStatusCode.OK)

                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Status token :" + respToken.StatusCode.ToString());
                                    if (respToken.StatusCode == HttpStatusCode.OK)
                                    {
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Token OK ");
                                        token = JsonConvert.DeserializeObject<Token>(str_token);
                                        _intExpire = token.expires_in;

                                        /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                        AddOrUpdateAppSetting("PixVariables:token", token.access_token);
                                        AddOrUpdateAppSetting("PixVariables:inicio", _dtmInicioToken);*/
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);

                                        if (token.access_token.Length > 0)
                                        {

                                            for (int i = 0; i < dtt_trans_pix.Rows.Count; i++)
                                            {
                                                // Associar o token aos headers do objeto
                                                // do tipo HttpClient
                                                client.DefaultRequestHeaders.Accept.Clear();
                                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Envia om para o banco ");
                                                //Envia om para o banco
                                                HttpResponseMessage response = client.GetAsync(dtt_trans_pix.Rows[0]["str_urlreg_pix"].ToString() + "/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString()).Result;
                                                //Novo endpoint
                                                //HttpResponseMessage response = client.GetAsync("https://pix-pj.api.itau.com/regulatorio-pix/v2" + "/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString()).Result;
                                                

                                                //Produção
                                                //HttpResponseMessage response = client.GetAsync(dtt_trans_pix.Rows[0]["str_urlreg_pix"].ToString() + "/v1/spi/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString()).Result;

                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Retornou Status " + response.StatusCode.ToString());
                                                if (response.StatusCode == HttpStatusCode.OK)
                                                {
                                                    str_ret = response.Content.ReadAsStringAsync().Result;
                                                    if (str_ret.Length > 0)
                                                    {
                                                        ConsQRCODE_237 reg_ret = JsonConvert.DeserializeObject<ConsQRCODE_237>(str_ret);
                                                        DataRow row = null;

                                                        using (SqlTransaction transaction = conn.BeginTransaction())
                                                        {
                                                            try
                                                            {
                                                                dtt_trans_grv = _repData.CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
                                                                row = dtt_trans_grv.NewRow();
                                                                row["id"] = dtt_trans_pix.Rows[i]["id"];
                                                                row["id_chavepix"] = dtt_trans_pix.Rows[i]["id_chavepix"];
                                                                row["str_txid"] = dtt_trans_pix.Rows[i]["str_txid"];
                                                                row["int_expiracao"] = dtt_trans_pix.Rows[i]["int_expiracao"];
                                                                row["int_cpf_dev"] = dtt_trans_pix.Rows[i]["int_cpf_dev"];
                                                                row["int_cnpj_dev"] = dtt_trans_pix.Rows[i]["int_cnpj_dev"];
                                                                row["str_nome_dev"] = dtt_trans_pix.Rows[i]["str_nome_dev"];
                                                                row["dbl_valor_orig"] = dtt_trans_pix.Rows[i]["dbl_valor_orig"];
                                                                row["str_msg_devedor"] = dtt_trans_pix.Rows[i]["str_msg_devedor"];
                                                                row["str_data_cria"] = reg_ret.calendario.criacao;
                                                                row["int_revisao"] = reg_ret.revisao;
                                                                row["str_location"] = reg_ret.location;
                                                                if (reg_ret.pix.Count > 0)
                                                                {
                                                                    row["int_cpf_pag"] = reg_ret.pix[0].pagador.cpf;
                                                                    row["int_cnpj_pag"] = reg_ret.pix[0].pagador.cnpj;
                                                                    row["str_nome_pag"] = reg_ret.pix[0].pagador.nome;
                                                                    row["str_msg_pagador"] = reg_ret.pix[0].infoPagador;
                                                                    if (reg_ret.pix[0].devolucoes.Count > 0)
                                                                    {
                                                                        row["str_id_devol"] = reg_ret.pix[0].devolucoes[0].Id;
                                                                        row["str_rtrid_devol"] = reg_ret.pix[0].devolucoes[0].rtrId;
                                                                        row["dbl_valor_devol"] = reg_ret.pix[0].devolucoes[0].valor;
                                                                        row["dtm_hora_sol_devol"] = reg_ret.pix[0].devolucoes[0].horario.solicitacao;
                                                                        row["dtm_hora_liq_devol"] = reg_ret.pix[0].devolucoes[0].horario.liquidacao;
                                                                        row["int_sit_devol"] = (reg_ret.pix[0].devolucoes[0].status == "EM_PROCESSAMENTO" ? 0 : (reg_ret.pix[0].devolucoes[0].status == "DEVOLVIDO" ? 1 : 2));
                                                                    }
                                                                    else
                                                                    {
                                                                        row["str_id_devol"] = 0;
                                                                        row["str_rtrid_devol"] = "";
                                                                        row["dbl_valor_devol"] = 0;
                                                                        row["dtm_hora_sol_devol"] = "";
                                                                        row["dtm_hora_liq_devol"] = "";
                                                                        row["int_sit_devol"] = -1;
                                                                    }
                                                                    row["str_idfim"] = reg_ret.pix[0].endToEndId;
                                                                }
                                                                else
                                                                {
                                                                    row["int_cpf_pag"] = 0;
                                                                    row["int_cnpj_pag"] = 0;
                                                                    row["str_nome_pag"] = "";
                                                                    row["str_msg_pagador"] = "";
                                                                    row["str_id_devol"] = 0;
                                                                    row["str_rtrid_devol"] = "";
                                                                    row["dbl_valor_devol"] = 0;
                                                                    row["dtm_hora_sol_devol"] = "";
                                                                    row["dtm_hora_liq_devol"] = "";
                                                                    row["int_sit_devol"] = -1;
                                                                    row["str_idfim"] = "";
                                                                }
                                                                row["int_filial"] = dtt_trans_pix.Rows[i]["int_filial"];
                                                                row["int_tipoped"] = dtt_trans_pix.Rows[i]["int_tipoped"];
                                                                row["int_pedido"] = dtt_trans_pix.Rows[i]["int_pedido"];
                                                                row["int_operador"] = dtt_trans_pix.Rows[i]["int_operador"];
                                                                row["int_caixa"] = dtt_trans_pix.Rows[i]["int_caixa"];
                                                                row["str_emv"] = dtt_trans_pix.Rows[i]["str_emv"];
                                                                row["int_situacao"] = (reg_ret.status == "ATIVA" ? 1 : (reg_ret.status == "CONCLUIDA" ? 2 : (reg_ret.status == "REMOVIDA_PELO_USUARIO_RECEBEDOR" ? 3 : 4)));
                                                                row["int_usu_lib"] = DBNull.Value;
                                                                row["int_usu_dev"] = DBNull.Value;

                                                                dtt_trans_grv.Rows.Add(row);

                                                                stbTran = _repData.SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = _repData.ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                transaction.Commit();

                                                                str_retorno = "Situação do PIX : " + (reg_ret.status == "ATIVA" ? "Em Aberto" : (reg_ret.status == "CONCLUIDA" ? "Pago" : (reg_ret.status == "REMOVIDA_PELO_USUARIO_RECEBEDOR" ? "Removido recebedor" : "Removido banco")));
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- PIX.Repositories - ConsultaPIX Erro : " + ex.Message.ToString());
                                                                transaction.Rollback();
                                                                str_retorno = ex.Message.ToString();
                                                            }
                                                        }

                                                        str_ret = "Alteração da Transação PIX efetuada com sucesso.";
                                                    }
                                                }
                                                else
                                                {

                                                    str_ret = "Código do erro : " + response.StatusCode.ToString() + " Mensagem: " + response.RequestMessage.ToString();
                                                }
                                            }

                                        }
                                    }
                                }
                                else
                                {
                                    str_ret = "Problemas na geração do Token.";
                                }

                            }
                        }
                        else
                        {
                            str_retorno = "Não existe pedido para ser registrado no banco";
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- PIX.Repositories - ConsultaPIX Erro 2: " + ex.Message.ToString());
                        conn.Close();
                        throw ex;
                    }
                    conn.Close();
                    return str_retorno;
                }
            }
            else
            {
                str_retorno = "Os campos Filial, Pedido, Tipo são obrogatórios.Chamada fora do padrão";
            }

            return str_retorno;
        }

        public string DevolucaoPIX(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_token = "";
            string str_msg = "";
            string str_id = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();
            List<Params> filtros = new List<Params>();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        filtros.AddRange(new List<Params>
                        {
                            new Params { nome = "id", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name },
                            new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "int_codfil", valor = pedido.Filial.ToString(), tipo = typeof(Int32).Name },
                            new Params { nome = "str_tipoped", valor = pedido.Tipo.ToString(), tipo = typeof(string).Name },
                            new Params { nome = "int_pedido", valor = pedido.numpedido.ToString(), tipo = typeof(Int64).Name },
                            new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name },
                            new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name },
                            new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name },
                            new Params { nome = "situacao", valor = Convert.ToInt16(StatusPIX.Devolver).ToString(), tipo = typeof(Int16).Name },
                            new Params { nome = "DtIni", valor = "2001-01-01", tipo = typeof(DateTime).Name },
                            new Params { nome = "DtFim", valor = "2001-01-01", tipo = typeof(DateTime).Name }
                        });
                        dtt_trans_pix = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Achou pedido (" + pedido.numpedido.ToString() + ")");

                            //Local homologação
                            //var cert = new X509Certificate2(@"D:\Jackson\Clientes\Guaibim\certificados\2026\pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            var cert = new X509Certificate2(@"./pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                            {
                                str_msg = "Certificado PIX Itaú (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";

                                List<envEmail> envMail = new List<envEmail>();
                                envMail.Add(new envEmail
                                {
                                    id = 0,
                                    id_assunto = 0,
                                    str_remet = _remet,
                                    str_dest = _dest,
                                    str_copias = _cc,
                                    str_copias_oc = _cco,
                                    str_corpo = str_msg,
                                    str_html = "N",
                                    str_anexo = "",
                                    dtm_inclusao = DateTime.Now.AddHours(-3),
                                    int_usuario = 696,
                                    str_erro = "",
                                    int_situacao = -1, //0 - Não enviado / 1 - Enviado / 2 - Com erro / -1 - Uma vez ao dia / -2 - Uma vez por semana / -3 - Uma vez por mês
                                    str_assunto = "Aviso de expiração de certificado - PIX"
                                });

                                str_retorno = ManutencaoTabela<envEmail>("I", envMail, "ntv_tbl_envio_emails", conn, null);

                                //str_ret = _repPIX.EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                            }
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Selecionou certificado " + cert.ToString());


                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(cert);

                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                            handler.ServerCertificateCustomValidationCallback +=
                                (httpRequestMessage, cert2, cetChain, policyErrors) =>
                                {
                                    return policyErrors == SslPolicyErrors.None;
                                };

                            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                                var dict = new Dictionary<string, string>();
                                dict.Add("grant_type", "client_credentials");
                                dict.Add("client_id", dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString());
                                dict.Add("client_secret", dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                str_token = respToken.Content.ReadAsStringAsync().Result;
                                _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                                /*
                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                //Dados do token inicio (08/11/2021 17:00:05) validade (3600) tempo decorrido (-88.23:59:54.2342864)
                                if (Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString()) < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                    respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                else
                                {
                                    token = new Token
                                    {
                                        //access_token = GetAppSetting("PixVariables:token"),
                                        access_token = obj_config["PixVariables"]["token"].ToString(),
                                        token_type = "Bearer",
                                        expires_in = Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString())
                                    };
                                    str_token = JsonConvert.SerializeObject(token);
                                }*/

                                if (str_token.Contains("access_token"))
                                {
                                    if (respToken.StatusCode == HttpStatusCode.OK)
                                    {

                                        token = JsonConvert.DeserializeObject<Token>(str_token);
                                        _intExpire = token.expires_in;

                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);

                                        if (token.access_token.Length > 0)
                                        {

                                            for (int i = 0; i < dtt_trans_pix.Rows.Count; i++)
                                            {
                                                // Associar o token aos headers do objeto
                                                // do tipo HttpClient
                                                client.DefaultRequestHeaders.Accept.Clear();
                                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                                str_id = "02" + dtt_trans_pix.Rows[i]["str_txid"].ToString().Substring(2, dtt_trans_pix.Rows[i]["str_txid"].ToString().Length - 2);
                                                str_json.Remove(0, str_json.Length);
                                                str_json.Append("{");
                                                str_json.Append("\"valor\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["dbl_valor_devol"].ToString().Replace(",", ".") + "\",");
                                                str_json.Append("\"natureza\"" + ":" + "\"ORIGINAL\",");
                                                str_json.Append("\"descricao\"" + ":" + "\"Conforme solicitado segue devolução do PIX.\"");

                                                if (i + 1 == dtt_trans_pix.Rows.Count)
                                                {
                                                    str_json.Append("}");
                                                }
                                                else
                                                {
                                                    str_json.Append("},");
                                                }

                                                //Envia om para o banco
                                                //Produção
                                                HttpResponseMessage response = client.PutAsync(dtt_trans_pix.Rows[0]["str_urlreg_pix"].ToString() + "/pix/" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "/devolucao/" + str_id, new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;
                                                //Novo endpoint
                                                //HttpResponseMessage response = client.PutAsync("https://pix-pj.api.itau.com/regulatorio-pix/v2" + "/pix/" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "/devolucao/" + str_id, new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;                                                


                                                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                                                {
                                                    str_ret = response.Content.ReadAsStringAsync().Result;
                                                    if (str_ret.Length > 0)
                                                    {
                                                        DevQRCODE_341 reg_ret = JsonConvert.DeserializeObject<DevQRCODE_341>(str_ret);
                                                        DataRow row = null;

                                                        using (SqlTransaction transaction = conn.BeginTransaction())
                                                        {
                                                            try
                                                            {
                                                                dtt_trans_grv = _repData.CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
                                                                row = dtt_trans_grv.NewRow();
                                                                row["id"] = dtt_trans_pix.Rows[i]["id"];
                                                                row["id_chavepix"] = dtt_trans_pix.Rows[i]["id_chavepix"];
                                                                row["str_txid"] = dtt_trans_pix.Rows[i]["str_txid"];
                                                                row["int_expiracao"] = dtt_trans_pix.Rows[i]["int_expiracao"];
                                                                row["int_cpf_dev"] = dtt_trans_pix.Rows[i]["int_cpf_dev"];
                                                                row["int_cnpj_dev"] = dtt_trans_pix.Rows[i]["int_cnpj_dev"];
                                                                row["str_nome_dev"] = dtt_trans_pix.Rows[i]["str_nome_dev"];
                                                                row["dbl_valor_orig"] = dtt_trans_pix.Rows[i]["dbl_valor_orig"];
                                                                row["str_msg_devedor"] = dtt_trans_pix.Rows[i]["str_msg_devedor"];
                                                                row["str_data_cria"] = Convert.ToDateTime(dtt_trans_pix.Rows[i]["str_data_cria"]).ToString("yyyy-MM-dd hh:mm:ss");
                                                                row["int_revisao"] = dtt_trans_pix.Rows[i]["int_revisao"];
                                                                row["str_location"] = dtt_trans_pix.Rows[i]["str_location"];
                                                                row["int_cpf_pag"] = dtt_trans_pix.Rows[i]["int_cpf_pag"];
                                                                row["int_cnpj_pag"] = dtt_trans_pix.Rows[i]["int_cnpj_pag"];
                                                                row["str_nome_pag"] = dtt_trans_pix.Rows[i]["str_nome_pag"];
                                                                row["str_msg_pagador"] = dtt_trans_pix.Rows[i]["str_msg_pagador"];
                                                                row["str_id_devol"] = str_id;
                                                                row["str_rtrid_devol"] = reg_ret.rtrid;
                                                                row["dbl_valor_devol"] = reg_ret.valor;
                                                                row["dtm_hora_sol_devol"] = reg_ret.horario.solicitacao;
                                                                row["dtm_hora_liq_devol"] = reg_ret.horario.liquidacao;
                                                                row["int_sit_devol"] = (reg_ret.status == "EM_PROCESSAMENTO" ? 0 : (reg_ret.status == "DEVOLVIDO" ? 9 : 2));
                                                                row["str_idfim"] = dtt_trans_pix.Rows[i]["str_idfim"];
                                                                row["int_filial"] = dtt_trans_pix.Rows[i]["int_filial"];
                                                                row["int_tipoped"] = dtt_trans_pix.Rows[i]["int_tipoped"];
                                                                row["int_pedido"] = dtt_trans_pix.Rows[i]["int_pedido"];
                                                                row["int_operador"] = dtt_trans_pix.Rows[i]["int_operador"];
                                                                row["int_caixa"] = dtt_trans_pix.Rows[i]["int_caixa"];
                                                                row["str_emv"] = dtt_trans_pix.Rows[i]["str_emv"];
                                                                row["int_situacao"] = StatusPIX.Devolvido;
                                                                row["int_usu_lib"] = DBNull.Value;
                                                                row["int_usu_dev"] = DBNull.Value;

                                                                dtt_trans_grv.Rows.Add(row);

                                                                stbTran = _repData.SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = _repData.ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                transaction.Commit();
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- PIX.Repositories - DevolucaoPIX Erro : " + ex.Message.ToString());
                                                                transaction.Rollback();
                                                                throw ex;
                                                            }
                                                        }

                                                        str_ret = "Alteração da Transação PIX efetuada com sucesso.";
                                                    }
                                                }
                                                else
                                                {

                                                    if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                                                    {
                                                        str_ret = response.Content.ReadAsStringAsync().Result;
                                                        str_retorno = str_ret;

                                                    }
                                                    else
                                                    {
                                                        str_ret = "Código do erro : " + response.StatusCode.ToString() + " Mensagem: " + response.RequestMessage.ToString();
                                                        str_retorno = str_ret;
                                                    }
                                                }
                                            }

                                        }
                                    }
                                }
                                else
                                {
                                    str_ret = "Problemas na geração do Token.";
                                }

                            }
                        }
                        else
                        {
                            str_retorno = "Não existe pedido para ser registrado no banco";
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- PIX.Repositories - DevolucaoPIX Erro 2: " + ex.Message.ToString());
                        conn.Close();
                        throw ex;
                    }

                    conn.Close();
                    return str_retorno;
                }
            }
            else
            {
                str_retorno = "Os campos Filial, Pedido, Tipo são obrogatórios.Chamada fora do padrão";
            }

            return str_retorno;
        }

        public string Async_DevolucaoPIX(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_token = "";
            string str_msg = "";
            string str_id = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();
            List<Params> filtros = new List<Params>();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        filtros.AddRange(new List<Params>
                        {
                            new Params { nome = "id", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name },
                            new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "int_codfil", valor = pedido.Filial.ToString(), tipo = typeof(Int32).Name },
                            new Params { nome = "str_tipoped", valor = pedido.Tipo.ToString(), tipo = typeof(string).Name },
                            new Params { nome = "int_pedido", valor = pedido.numpedido.ToString(), tipo = typeof(Int64).Name },
                            new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name },
                            new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name },
                            new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name },
                            //new Params { nome = "situacao", valor = Convert.ToInt16(StatusPIX.Devolver).ToString(), tipo = typeof(Int16).Name },
                            new Params { nome = "situacao", valor = "2", tipo = typeof(Int16).Name },
                            new Params { nome = "DtIni", valor = "2001-01-01", tipo = typeof(DateTime).Name },
                            new Params { nome = "DtFim", valor = "2001-01-01", tipo = typeof(DateTime).Name }
                        });
                        dtt_trans_pix = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Devolução Achou pedido (" + pedido.numpedido.ToString() + ")");

                            //Local homologação
                            //var cert = new X509Certificate2(@"D:\Jackson\Clientes\Guaibim\certificados\2026\pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            var cert = new X509Certificate2(@"./pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                            {
                                str_msg = "Certificado PIX Itaú (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";

                                List<envEmail> envMail = new List<envEmail>();
                                envMail.Add(new envEmail
                                {
                                    id = 0,
                                    id_assunto = 0,
                                    str_remet = _remet,
                                    str_dest = _dest,
                                    str_copias = _cc,
                                    str_copias_oc = _cco,
                                    str_corpo = str_msg,
                                    str_html = "N",
                                    str_anexo = "",
                                    dtm_inclusao = DateTime.Now.AddHours(-3),
                                    int_usuario = 696,
                                    str_erro = "",
                                    int_situacao = -1, //0 - Não enviado / 1 - Enviado / 2 - Com erro / -1 - Uma vez ao dia / -2 - Uma vez por semana / -3 - Uma vez por mês
                                    str_assunto = "Aviso de expiração de certificado - PIX"
                                });

                                str_retorno = ManutencaoTabela<envEmail>("I", envMail, "ntv_tbl_envio_emails", conn, null);

                                //str_ret = _repPIX.EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                            }
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Devolução Selecionou certificado " + cert.ToString());


                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(cert);

                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                            handler.ServerCertificateCustomValidationCallback +=
                                (httpRequestMessage, cert2, cetChain, policyErrors) =>
                                {
                                    return policyErrors == SslPolicyErrors.None;
                                };

                            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                                var dict = new Dictionary<string, string>();
                                dict.Add("grant_type", "client_credentials");
                                dict.Add("client_id", dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString());
                                dict.Add("client_secret", dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                str_token = respToken.Content.ReadAsStringAsync().Result;
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- token " + str_token);
                                _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                                /*
                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                //Dados do token inicio (08/11/2021 17:00:05) validade (3600) tempo decorrido (-88.23:59:54.2342864)
                                if (Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString()) < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                    respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                else
                                {
                                    token = new Token
                                    {
                                        //access_token = GetAppSetting("PixVariables:token"),
                                        access_token = obj_config["PixVariables"]["token"].ToString(),
                                        token_type = "Bearer",
                                        expires_in = Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString())
                                    };
                                    str_token = JsonConvert.SerializeObject(token);
                                }*/

                                if (str_token.Contains("access_token"))
                                {
                                    if (respToken.StatusCode == HttpStatusCode.OK)
                                    {

                                        token = JsonConvert.DeserializeObject<Token>(str_token);
                                        _intExpire = token.expires_in;

                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);

                                        if (token.access_token.Length > 0)
                                        {

                                            for (int i = 0; i < dtt_trans_pix.Rows.Count; i++)
                                            {
                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Devolução monta JSON ");
                                                // Associar o token aos headers do objeto
                                                // do tipo HttpClient
                                                client.DefaultRequestHeaders.Accept.Clear();
                                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                                str_id = "02" + dtt_trans_pix.Rows[i]["str_txid"].ToString().Substring(2, dtt_trans_pix.Rows[i]["str_txid"].ToString().Length - 2);
                                                str_json.Remove(0, str_json.Length);
                                                str_json.Append("{");
                                                str_json.Append("\"valor\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["dbl_valor_devol"].ToString().Replace(",", ".") + "\",");
                                                str_json.Append("\"natureza\"" + ":" + "\"ORIGINAL\",");
                                                str_json.Append("\"descricao\"" + ":" + "\"Conforme solicitado segue devolução do PIX.\"");

                                                if (i + 1 == dtt_trans_pix.Rows.Count)
                                                {
                                                    str_json.Append("}");
                                                }
                                                else
                                                {
                                                    str_json.Append("},");
                                                }

                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Devolução Envia om para o banco " + str_json.ToString());
                                                //Envia om para o banco
                                                //Produção
                                                HttpResponseMessage response = client.PutAsync(dtt_trans_pix.Rows[0]["str_urlreg_pix"].ToString() + "/pix/" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "/devolucao/" + str_id, new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;
                                                //Novo endpoint
                                                //HttpResponseMessage response = client.PutAsync("https://pix-pj.api.itau.com/regulatorio-pix/v2" + "/pix/" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "/devolucao/" + str_id, new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;


                                                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                                                {
                                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Devolução retorno banco " + response.Content.ReadAsStringAsync().Result.ToString());
                                                    str_ret = response.Content.ReadAsStringAsync().Result;
                                                    if (str_ret.Length > 0)
                                                    {
                                                        DevQRCODE_341 reg_ret = JsonConvert.DeserializeObject<DevQRCODE_341>(str_ret);
                                                        DataRow row = null;

                                                        using (SqlTransaction transaction = conn.BeginTransaction())
                                                        {
                                                            try
                                                            {
                                                                dtt_trans_grv = _repData.CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
                                                                row = dtt_trans_grv.NewRow();
                                                                row["id"] = dtt_trans_pix.Rows[i]["id"];
                                                                row["id_chavepix"] = dtt_trans_pix.Rows[i]["id_chavepix"];
                                                                row["str_txid"] = dtt_trans_pix.Rows[i]["str_txid"];
                                                                row["int_expiracao"] = dtt_trans_pix.Rows[i]["int_expiracao"];
                                                                row["int_cpf_dev"] = dtt_trans_pix.Rows[i]["int_cpf_dev"];
                                                                row["int_cnpj_dev"] = dtt_trans_pix.Rows[i]["int_cnpj_dev"];
                                                                row["str_nome_dev"] = dtt_trans_pix.Rows[i]["str_nome_dev"];
                                                                row["dbl_valor_orig"] = dtt_trans_pix.Rows[i]["dbl_valor_orig"];
                                                                row["str_msg_devedor"] = dtt_trans_pix.Rows[i]["str_msg_devedor"];
                                                                row["str_data_cria"] = Convert.ToDateTime(dtt_trans_pix.Rows[i]["str_data_cria"]).ToString("yyyy-MM-dd hh:mm:ss");
                                                                row["int_revisao"] = dtt_trans_pix.Rows[i]["int_revisao"];
                                                                row["str_location"] = dtt_trans_pix.Rows[i]["str_location"];
                                                                row["int_cpf_pag"] = dtt_trans_pix.Rows[i]["int_cpf_pag"];
                                                                row["int_cnpj_pag"] = dtt_trans_pix.Rows[i]["int_cnpj_pag"];
                                                                row["str_nome_pag"] = dtt_trans_pix.Rows[i]["str_nome_pag"];
                                                                row["str_msg_pagador"] = dtt_trans_pix.Rows[i]["str_msg_pagador"];
                                                                row["str_id_devol"] = str_id;
                                                                row["str_rtrid_devol"] = reg_ret.rtrid;
                                                                row["dbl_valor_devol"] = reg_ret.valor;
                                                                row["dtm_hora_sol_devol"] = reg_ret.horario.solicitacao;
                                                                row["dtm_hora_liq_devol"] = reg_ret.horario.liquidacao;
                                                                row["int_sit_devol"] = (reg_ret.status == "EM_PROCESSAMENTO" ? 5 : (reg_ret.status == "DEVOLVIDO" ? 9 : 2));
                                                                row["str_idfim"] = dtt_trans_pix.Rows[i]["str_idfim"];
                                                                row["int_filial"] = dtt_trans_pix.Rows[i]["int_filial"];
                                                                row["int_tipoped"] = dtt_trans_pix.Rows[i]["int_tipoped"];
                                                                row["int_pedido"] = dtt_trans_pix.Rows[i]["int_pedido"];
                                                                row["int_operador"] = dtt_trans_pix.Rows[i]["int_operador"];
                                                                row["int_caixa"] = dtt_trans_pix.Rows[i]["int_caixa"];
                                                                row["str_emv"] = dtt_trans_pix.Rows[i]["str_emv"];
                                                                row["int_situacao"] = (reg_ret.status == "EM_PROCESSAMENTO" ? 5 : (reg_ret.status == "DEVOLVIDO" ? 9 : dtt_trans_pix.Rows[i]["int_situacao"]));
                                                                row["int_usu_lib"] = DBNull.Value;
                                                                row["int_usu_dev"] = DBNull.Value;

                                                                dtt_trans_grv.Rows.Add(row);

                                                                stbTran = _repData.SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = _repData.ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                transaction.Commit();
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- PIX.Repositories - DevolucaoPIX Erro : " + ex.Message.ToString());
                                                                transaction.Rollback();
                                                                throw ex;
                                                            }
                                                        }

                                                        str_ret = "Alteração da Transação PIX efetuada com sucesso.";
                                                    }
                                                }
                                                else
                                                {

                                                    if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                                                    {
                                                        str_ret = response.Content.ReadAsStringAsync().Result;
                                                        str_retorno = str_ret;

                                                    }
                                                    else
                                                    {
                                                        str_ret = "Código do erro : " + response.StatusCode.ToString() + " Mensagem: " + response.RequestMessage.ToString();
                                                        str_retorno = str_ret;
                                                    }
                                                }
                                            }

                                        }
                                    }
                                }
                                else
                                {
                                    str_ret = "Problemas na geração do Token.";
                                }

                            }
                        }
                        else
                        {
                            str_retorno = "Não existe pedido para ser registrado no banco";
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- PIX.Repositories - DevolucaoPIX Erro 2: " + ex.Message.ToString());
                        conn.Close();
                        throw ex;
                    }

                    conn.Close();
                    return str_retorno;
                }
            }
            else
            {
                str_retorno = "Os campos Filial, Pedido, Tipo são obrogatórios.Chamada fora do padrão";
            }

            return str_retorno;
        }

        public string RegistraPedidoPIX(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_token = "";
            string str_emv = "";
            string str_msg = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_pixs = new DataTable();
            DataTable dtt_trans_itens = new DataTable();
            DataTable dtt_trans_grv = new DataTable();

            Boolean bol_registra = false;
            Boolean bol_pago = false;

            Int64 id_pix = 0;

            HttpResponseMessage respToken = new HttpResponseMessage();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                //Identifica se o pedido tem 2 PIX´s                
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Registra QR CODE pedido (" + pedido.numpedido.ToString() + ")");
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();

                    dtt_pixs = _repPIX.ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Todos, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);

                    if (dtt_pixs.Rows.Count > 1)
                    {
                        for (int ped = 0; ped < dtt_pixs.Rows.Count; ped++)
                        {
                            if (ped == 0)
                            {
                                if (Convert.ToInt16(dtt_pixs.Rows[ped]["int_situacao"]) == 0)
                                {
                                    bol_pago = false;
                                    bol_registra = true;
                                    id_pix = Convert.ToInt64(dtt_pixs.Rows[0]["id"]);
                                    break;
                                }
                                else
                                {
                                    if (Convert.ToInt16(dtt_pixs.Rows[ped]["int_situacao"]) == 2)
                                    {
                                        bol_pago = true;
                                    }
                                }
                            }
                            else
                            {
                                //caso tenha dosi PIX´s identificar se o primeiro foi pago
                                if (Convert.ToInt16(dtt_pixs.Rows[ped]["int_situacao"]) == 0 && bol_pago)
                                {
                                    bol_registra = true;
                                    id_pix = Convert.ToInt64(dtt_pixs.Rows[ped]["id"]);
                                }
                                else
                                {
                                    bol_registra = false;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (dtt_pixs.Rows.Count > 0)
                        {
                            bol_registra = true;
                            id_pix = Convert.ToInt64(dtt_pixs.Rows[0]["id"]);
                        }
                        else
                        {
                            bol_registra = false;
                            id_pix = 0;
                        }
                    }
                    if (bol_registra)
                    {
                        using (SqlTransaction transaction = conn.BeginTransaction())
                        {
                            try
                            {

                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Consulta pedido (" + pedido.numpedido.ToString() + ")");
                                dtt_trans_pix = _repPIX.ConsultaTransacaoPIX(id_pix, 0, -2, conn, transaction);

                                if (dtt_trans_pix.Rows.Count > 0)
                                {
                                    
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Achou pedido (" + pedido.numpedido.ToString() + ")");

                                    //Local homologação
                                    //var cert = new X509Certificate2(@"D:\Jackson\Clientes\Guaibim\certificados\2026\pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                                    var cert = new X509Certificate2(@"./pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Carregou cerificado (" + cert.ToString() + ")");

                                    if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                                    {
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- monta mensagem");
                                        str_msg = "Certificado PIX Itaú (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";

                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- mensagem (" + str_msg + ")");
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- data e hora (" + DateTime.Now.AddHours(-3) + ")");

                                        List<envEmail> envMail = new List<envEmail>();
                                        envMail.Add(new envEmail
                                        {
                                            id = 0,
                                            id_assunto = 0,
                                            str_remet = _remet,
                                            str_dest = _dest,
                                            str_copias = _cc,
                                            str_copias_oc = _cco,
                                            str_corpo = str_msg,
                                            str_html = "N",
                                            str_anexo = "",
                                            dtm_inclusao = DateTime.Now.AddHours(-3),
                                            int_usuario = 696,
                                            str_erro = "",
                                            int_situacao = -1, //0 - Não enviado / 1 - Enviado / 2 - Com erro / -1 - Uma vez ao dia / -2 - Uma vez por semana / -3 - Uma vez por mês
                                            str_assunto = "Aviso de expiração de certificado - PIX"
                                        });

                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Gravar e-mail (" + envMail.ToString() + ")");

                                        str_retorno = ManutencaoTabela<envEmail>("I", envMail, "ntv_tbl_envio_emails", conn, transaction);

                                        //str_ret = _repPIX.EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                                    }
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Selecionou certificado " + cert.ToString());
                                    

                                    var handler = new HttpClientHandler();
                                    handler.ClientCertificates.Add(cert);

                                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                                    handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                                    handler.ServerCertificateCustomValidationCallback +=
                                        (httpRequestMessage, cert2, cetChain, policyErrors) =>
                                        {
                                            return policyErrors == SslPolicyErrors.None;
                                        };

                                    handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                                    using (var client = new HttpClient(handler))
                                    {

                                        //Busca autorização
                                        str_json.Remove(0, str_json.Length);
                                        client.DefaultRequestHeaders.Accept.Clear();
                                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                                        var dict = new Dictionary<string, string>();
                                        dict.Add("grant_type", "client_credentials");
                                        dict.Add("client_id", dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString());
                                        dict.Add("client_secret", dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                        FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);
                                        _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                                        //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();

                                        TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                        //Dados do token inicio (08/11/2021 17:00:05) validade (3600) tempo decorrido (-88.23:59:54.2342864)
                                        /*if (Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString()) < Convert.ToInt64(tempo.TotalSeconds))
                                        {
                                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                            respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                            str_token = respToken.Content.ReadAsStringAsync().Result;
                                            _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                        else
                                        {
                                            token = new Token
                                            {
                                                //access_token = GetAppSetting("PixVariables:token"),
                                                access_token = obj_config["PixVariables"]["token"].ToString(),
                                                token_type = "Bearer",
                                                expires_in = Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString())
                                            };
                                            str_token = JsonConvert.SerializeObject(token);
                                        }*/

                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                        respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                        str_token = respToken.Content.ReadAsStringAsync().Result;
                                        _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");


                                        if (str_token.Contains("access_token"))
                                        {
                                            if (respToken.StatusCode == HttpStatusCode.OK)
                                            {

                                                token = JsonConvert.DeserializeObject<Token>(str_token);
                                                _intExpire = token.expires_in;

                                                //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                                //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                                //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);


                                                if (token.access_token.Length > 0)
                                                {

                                                    for (int i = 0; i < dtt_trans_pix.Rows.Count; i++)
                                                    {
                                                        // Associar o token aos headers do objeto
                                                        // do tipo HttpClient
                                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Monta JSON de envio ");
                                                        client.DefaultRequestHeaders.Accept.Clear();
                                                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                                        str_json.Remove(0, str_json.Length);
                                                        str_json.Append("{");

                                                        str_json.Append("\"calendario\"" + ":{");
                                                        str_json.Append("\"expiracao\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["int_expiracao"].ToString() + "\"");
                                                        str_json.Append("},");

                                                        str_json.Append("\"devedor\"" + ":{");
                                                        if (Convert.ToInt64(dtt_trans_pix.Rows[0]["int_cpf_dev"].ToString()) > 0)
                                                        {
                                                            str_json.Append("\"cpf\"" + ":" + "\"" + ("00000000000" + dtt_trans_pix.Rows[0]["int_cpf_dev"].ToString()).Substring(("00000000000" + dtt_trans_pix.Rows[0]["int_cpf_dev"].ToString()).Length - 11, 11) + "\",");
                                                            str_json.Append("\"nome\"" + ":" + "\"" + (dtt_trans_pix.Rows[0]["str_nome_dev"].ToString().Length > 0 ? dtt_trans_pix.Rows[0]["str_nome_dev"].ToString().Replace("/", " ") : "Jackson Vago") + "\"");
                                                        }
                                                        else
                                                        {
                                                            str_json.Append("\"cnpj\"" + ":" + "\"" + ("00000000000000" + dtt_trans_pix.Rows[0]["int_cnpj_dev"].ToString()).Substring(("00000000000000" + dtt_trans_pix.Rows[0]["int_cnpj_dev"].ToString()).Length - 14, 14) + "\",");
                                                            str_json.Append("\"nome\"" + ":" + "\"" + (dtt_trans_pix.Rows[0]["str_nome_dev"].ToString().Length > 0 ? dtt_trans_pix.Rows[0]["str_nome_dev"].ToString().Replace("/", " ") : "Jackson Vago") + "\"");
                                                        }

                                                        str_json.Append("},");

                                                        str_json.Append("\"valor\"" + ":{");
                                                        str_json.Append("\"original\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["dbl_valor_orig"].ToString().Replace(",", ".") + "\"");
                                                        str_json.Append("},");

                                                        str_json.Append("\"chave\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_chavepix"].ToString() + "\"");

                                                        if (dtt_trans_pix.Rows[0]["str_msg_devedor"].ToString().Length > 0)
                                                        {
                                                            str_json.Append(",\"solicitacaopagador\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["str_msg_devedor"].ToString() + "\"");
                                                        }

                                                        dtt_trans_itens = _repPIX.ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Ativa, 1, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn, transaction);


                                                        if (dtt_trans_itens.Rows.Count > 0)
                                                        {
                                                            str_json.Append(", \"info_adicionais\"" + ":[");
                                                            for (int linf = 0; linf < dtt_trans_itens.Rows.Count; linf++)
                                                            {
                                                                str_json.Append("{");
                                                                str_json.Append("\"nome\"" + ":" + "\"" + dtt_trans_itens.Rows[linf]["descricao"].ToString().Replace("/", " ").Replace("\"", "") + "\",");
                                                                str_json.Append("\"valor\"" + ":" + "\"" + dtt_trans_itens.Rows[linf]["vltotitem"].ToString().Replace(",", ".") + "\"");

                                                                if (linf + 1 == dtt_trans_itens.Rows.Count)
                                                                {
                                                                    str_json.Append("}");
                                                                }
                                                                else
                                                                {
                                                                    str_json.Append("},");
                                                                }

                                                            }
                                                            str_json.Append("]");
                                                        }


                                                        if (i + 1 == dtt_trans_pix.Rows.Count)
                                                        {
                                                            str_json.Append("}");
                                                        }
                                                        else
                                                        {
                                                            str_json.Append("},");
                                                        }

                                                        //Envia om para o banco
                                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Vai enviar pedido " + str_json.ToString());

                                                        HttpResponseMessage response = client.PutAsync(dtt_trans_pix.Rows[0]["str_urlreg_pix"].ToString() + "/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;
                                                        //Novo endpoint
                                                        //HttpResponseMessage response = client.PutAsync("https://pix-pj.api.itau.com/regulatorio-pix/v2" + "/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;
                                                        //Homologação
                                                        //HttpResponseMessage response = client.PutAsync("https://sandbox.devportal.itau.com.br/itau-ep9-api-regulatorio-pix-v2-externo/v2" + "/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;
                                                        
                                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Retorno do envio " + response.ToString());

                                                        if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                                                        {
                                                            str_ret = response.Content.ReadAsStringAsync().Result;
                                                            if (str_ret.Length > 0)
                                                            {
                                                                RegQRCODE_237 reg_ret = JsonConvert.DeserializeObject<RegQRCODE_237>(str_ret);
                                                                DataRow row = null;

                                                                if (reg_ret.status == "ATIVA")
                                                                {
                                                                    dtt_trans_pix.Rows[i]["str_location"] = reg_ret.location;
                                                                    str_emv = _repPIX.GeraStringQRCODE(dtt_trans_pix.Rows[i]);
                                                                    //str_ret = GeraImagemQRCODE(str_ret);
                                                                    str_ret = "Transação PIX registrada.";
                                                                }
                                                                else
                                                                {
                                                                    str_ret = "Transação PIX não registrada.";
                                                                }

                                                                dtt_trans_grv = _repPIX.CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
                                                                row = dtt_trans_grv.NewRow();
                                                                row["id"] = dtt_trans_pix.Rows[i]["id"];
                                                                row["id_chavepix"] = dtt_trans_pix.Rows[i]["id_chavepix"];
                                                                row["str_txid"] = dtt_trans_pix.Rows[i]["str_txid"];
                                                                row["int_expiracao"] = dtt_trans_pix.Rows[i]["int_expiracao"];
                                                                row["int_cpf_dev"] = dtt_trans_pix.Rows[i]["int_cpf_dev"];
                                                                row["int_cnpj_dev"] = dtt_trans_pix.Rows[i]["int_cnpj_dev"];
                                                                row["str_nome_dev"] = dtt_trans_pix.Rows[i]["str_nome_dev"];
                                                                row["dbl_valor_orig"] = dtt_trans_pix.Rows[i]["dbl_valor_orig"];
                                                                row["str_msg_devedor"] = dtt_trans_pix.Rows[i]["str_msg_devedor"];
                                                                row["str_data_cria"] = reg_ret.calendario.criacao;
                                                                row["int_revisao"] = reg_ret.revisao;
                                                                row["str_location"] = reg_ret.location;
                                                                row["int_cpf_pag"] = 0;
                                                                row["int_cnpj_pag"] = 0;
                                                                row["str_nome_pag"] = "";
                                                                row["str_msg_pagador"] = "";
                                                                row["str_id_devol"] = 0;
                                                                row["str_rtrid_devol"] = 0;
                                                                row["dbl_valor_devol"] = 0;
                                                                row["dtm_hora_sol_devol"] = "";
                                                                row["dtm_hora_liq_devol"] = "";
                                                                row["int_sit_devol"] = 0;
                                                                row["str_idfim"] = "";
                                                                row["int_filial"] = dtt_trans_pix.Rows[i]["int_filial"];
                                                                row["int_tipoped"] = dtt_trans_pix.Rows[i]["int_tipoped"];
                                                                row["int_pedido"] = dtt_trans_pix.Rows[i]["int_pedido"];
                                                                row["int_operador"] = dtt_trans_pix.Rows[i]["int_operador"];
                                                                row["int_caixa"] = dtt_trans_pix.Rows[i]["int_caixa"];
                                                                row["str_emv"] = str_emv;
                                                                row["int_situacao"] = (reg_ret.status == "ATIVA" ? 1 : (reg_ret.status == "CONCLUIDA" ? 2 : (reg_ret.status == "REMOVIDA_PELO_USUARIO_RECEBEDOR" ? 3 : 4)));
                                                                row["int_usu_lib"] = DBNull.Value;
                                                                row["int_usu_dev"] = DBNull.Value;

                                                                dtt_trans_grv.Rows.Add(row);

                                                                stbTran = _repPIX.SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = _repPIX.ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                _intExpire = reg_ret.calendario.expiracao;
                                                                //_dtmInicioToken = Convert.ToDateTime(reg_ret.calendario.criacao);

                                                            }
                                                        }
                                                        else
                                                        {

                                                            if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                                                            {
                                                                str_ret = response.Content.ReadAsStringAsync().Result;
                                                                str_retorno = str_ret;
                                                                str_ret = "Código do erro : " + response.StatusCode.ToString() + " Mensagem: " + str_ret;
                                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Erro no envio : " + str_ret);
                                                            }
                                                            else
                                                            {
                                                                str_ret = response.Content.ReadAsStringAsync().Result;
                                                                if (str_ret == null)
                                                                {
                                                                    str_ret = "";
                                                                }
                                                                str_ret += " Código do erro : " + response.StatusCode.ToString() + " Mensagem: " + response.RequestMessage.ToString();
                                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Erro no envio : " + str_ret);
                                                                str_retorno = str_ret;
                                                            }
                                                        }
                                                    }

                                                }
                                            }
                                        }
                                        else
                                        {
                                            str_ret = "Problemas na geração do Token (" + str_token + ")";
                                            _logger.LogInformation(DateTime.Now.ToString("G") + " -  " + str_ret);
                                        }

                                    }
                                }
                                else
                                {
                                    str_retorno = "Não existe pedido para ser registrado no banco";
                                }

                                if (str_retorno.Contains("sucesso") || str_retorno.Contains("ok"))
                                {
                                    transaction.Commit();
                                }
                                else
                                {
                                    transaction.Rollback();
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- RegistraPedidoPIX  erro: " + ex.Message.ToString());
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- RegistraPedidoPIX Inner exception: " + ex.InnerException.Message.ToString());
                                transaction.Rollback();
                                conn.Close();
                                str_retorno = ex.Message.ToString();
                            }
                        }
                    }
                    conn.Close();
                }
            }
            else
            {
                str_retorno = "Os campos Filial, Pedido, Tipo são obrogatórios.Chamada fora do padrão";
            }

            return str_retorno;
        }

        public string RegistraTituloPIX(Int64 id_pix)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";
            string str_emv = "";
            string str_msg = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_pixs = new DataTable();
            DataTable dtt_trans_itens = new DataTable();
            DataTable dtt_trans_grv = new DataTable();

            Boolean bol_registra = false;
            Boolean bol_pago = false;

            HttpResponseMessage respToken = new HttpResponseMessage();

            if (id_pix > 0)
            {
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Registra QR CODE titulo (" + id_pix.ToString() + ")");
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {

                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Consulta Titulo (" + id_pix.ToString() + ")");
                            dtt_trans_pix = _repPIX.ConsultaTransacaoPIX(id_pix, "9", 0, -2, conn, transaction);

                            if (dtt_trans_pix.Rows.Count > 0)
                            {
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Achou pedido (" + id_pix.ToString() + ")");

                                //Local homologação
                                //var cert = new X509Certificate2(@"D:\Jackson\Clientes\Guaibim\certificados\2026\pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                                var cert = new X509Certificate2(@"./pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                                if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                                {
                                    str_msg = "Certificado PIX Itaú (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";

                                    List<envEmail> envMail = new List<envEmail>();
                                    envMail.Add(new envEmail
                                    {
                                        id = 0,
                                        id_assunto = 0,
                                        str_remet = _remet,
                                        str_dest = _dest,
                                        str_copias = _cc,
                                        str_copias_oc = _cco,
                                        str_corpo = str_msg,
                                        str_html = "N",
                                        str_anexo = "",
                                        dtm_inclusao = DateTime.Now.AddHours(-3),
                                        int_usuario = 696,
                                        str_erro = "",
                                        int_situacao = -1, //0 - Não enviado / 1 - Enviado / 2 - Com erro / -1 - Uma vez ao dia / -2 - Uma vez por semana / -3 - Uma vez por mês
                                        str_assunto = "Aviso de expiração de certificado - PIX"
                                    });

                                    str_retorno = ManutencaoTabela<envEmail>("I", envMail, "ntv_tbl_envio_emails", conn, transaction);

                                    //str_ret = _repPIX.EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                                }
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Selecionou certificado " + cert.ToString());


                                var handler = new HttpClientHandler();
                                handler.ClientCertificates.Add(cert);

                                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                                handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                                handler.ServerCertificateCustomValidationCallback +=
                                    (httpRequestMessage, cert2, cetChain, policyErrors) =>
                                    {
                                        return policyErrors == SslPolicyErrors.None;
                                    };

                                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                                using (var client = new HttpClient(handler))
                                {

                                    //Busca autorização
                                    str_json.Remove(0, str_json.Length);
                                    client.DefaultRequestHeaders.Accept.Clear();
                                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                                    var dict = new Dictionary<string, string>();
                                    dict.Add("grant_type", "client_credentials");
                                    dict.Add("client_id", dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString());
                                    dict.Add("client_secret", dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                    FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                                    //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();

                                    TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                    //Dados do token inicio (08/11/2021 17:00:05) validade (3600) tempo decorrido (-88.23:59:54.2342864)
                                    /*if (Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString()) < Convert.ToInt64(tempo.TotalSeconds))
                                    {
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                        respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                        str_token = respToken.Content.ReadAsStringAsync().Result;
                                        _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                    else
                                    {
                                        token = new Token
                                        {
                                            //access_token = GetAppSetting("PixVariables:token"),
                                            access_token = obj_config["PixVariables"]["token"].ToString(),
                                            token_type = "Bearer",
                                            expires_in = Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString())
                                        };
                                        str_token = JsonConvert.SerializeObject(token);
                                    }*/

                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                    respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");


                                    if (str_token.Contains("access_token"))
                                    {
                                        if (respToken.StatusCode == HttpStatusCode.OK)
                                        {

                                            token = JsonConvert.DeserializeObject<Token>(str_token);
                                            _intExpire = token.expires_in;

                                            //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                            //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                            //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);


                                            if (token.access_token.Length > 0)
                                            {

                                                for (int i = 0; i < dtt_trans_pix.Rows.Count; i++)
                                                {
                                                    // Associar o token aos headers do objeto
                                                    // do tipo HttpClient
                                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Monta JSON de envio ");
                                                    client.DefaultRequestHeaders.Accept.Clear();
                                                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                                    str_json.Remove(0, str_json.Length);
                                                    str_json.Append("{");

                                                    str_json.Append("\"calendario\"" + ":{");
                                                    str_json.Append("\"expiracao\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["int_expiracao"].ToString() + "\"");
                                                    str_json.Append("},");

                                                    str_json.Append("\"devedor\"" + ":{");
                                                    if (Convert.ToInt64(dtt_trans_pix.Rows[0]["int_cpf_dev"].ToString()) > 0)
                                                    {
                                                        str_json.Append("\"cpf\"" + ":" + "\"" + ("00000000000" + dtt_trans_pix.Rows[0]["int_cpf_dev"].ToString()).Substring(("00000000000" + dtt_trans_pix.Rows[0]["int_cpf_dev"].ToString()).Length - 11, 11) + "\",");
                                                        str_json.Append("\"nome\"" + ":" + "\"" + (dtt_trans_pix.Rows[0]["str_nome_dev"].ToString().Length > 0 ? dtt_trans_pix.Rows[0]["str_nome_dev"].ToString().Replace("/", " ") : "Jackson Vago") + "\"");
                                                    }
                                                    else
                                                    {
                                                        str_json.Append("\"cnpj\"" + ":" + "\"" + ("00000000000000" + dtt_trans_pix.Rows[0]["int_cnpj_dev"].ToString()).Substring(("00000000000000" + dtt_trans_pix.Rows[0]["int_cnpj_dev"].ToString()).Length - 14, 14) + "\",");
                                                        str_json.Append("\"nome\"" + ":" + "\"" + (dtt_trans_pix.Rows[0]["str_nome_dev"].ToString().Length > 0 ? dtt_trans_pix.Rows[0]["str_nome_dev"].ToString().Replace("/", " ") : "Jackson Vago") + "\"");
                                                    }

                                                    str_json.Append("},");

                                                    str_json.Append("\"valor\"" + ":{");
                                                    str_json.Append("\"original\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["dbl_valor_orig"].ToString().Replace(",", ".") + "\"");
                                                    str_json.Append("},");

                                                    str_json.Append("\"chave\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_chavepix"].ToString() + "\"");

                                                    if (dtt_trans_pix.Rows[0]["str_msg_devedor"].ToString().Length > 0)
                                                    {
                                                        str_json.Append(",\"solicitacaopagador\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["str_msg_devedor"].ToString() + "\"");
                                                    }

                                                    dtt_trans_itens = _repPIX.ConsultaTransacaoPIX(1, "9", id_pix, StatusPIX.Ativa, 1, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn, transaction);


                                                    if (dtt_trans_itens.Rows.Count > 0)
                                                    {
                                                        str_json.Append(", \"info_adicionais\"" + ":[");
                                                        for (int linf = 0; linf < dtt_trans_itens.Rows.Count; linf++)
                                                        {
                                                            str_json.Append("{");
                                                            str_json.Append("\"nome\"" + ":" + "\"" + dtt_trans_itens.Rows[linf]["descricao"].ToString().Replace("/", " ").Replace("\"", "") + "\",");
                                                            str_json.Append("\"valor\"" + ":" + "\"" + dtt_trans_itens.Rows[linf]["vltotitem"].ToString().Replace(",", ".") + "\"");

                                                            if (linf + 1 == dtt_trans_itens.Rows.Count)
                                                            {
                                                                str_json.Append("}");
                                                            }
                                                            else
                                                            {
                                                                str_json.Append("},");
                                                            }

                                                        }
                                                        str_json.Append("]");
                                                    }


                                                    if (i + 1 == dtt_trans_pix.Rows.Count)
                                                    {
                                                        str_json.Append("}");
                                                    }
                                                    else
                                                    {
                                                        str_json.Append("},");
                                                    }

                                                    //Envia om para o banco
                                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Vai enviar pedido " + str_json.ToString());

                                                    //HttpResponseMessage response = client.PostAsync(dtt_trans_pix.Rows[0]["str_urlreg_pix"].ToString() + "/cob", new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;
                                                    HttpResponseMessage response = client.PutAsync(dtt_trans_pix.Rows[0]["str_urlreg_pix"].ToString() + "/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;
                                                    //Novo endpoint
                                                    //HttpResponseMessage response = client.PutAsync("https://pix-pj.api.itau.com/regulatorio-pix/v2" + "/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;                                                    

                                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Retorno do envio " + response.ToString());

                                                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                                                    {
                                                        str_ret = response.Content.ReadAsStringAsync().Result;
                                                        if (str_ret.Length > 0)
                                                        {
                                                            RegQRCODE_237 reg_ret = JsonConvert.DeserializeObject<RegQRCODE_237>(str_ret);
                                                            DataRow row = null;

                                                            if (reg_ret.status == "ATIVA")
                                                            {
                                                                dtt_trans_pix.Rows[i]["str_location"] = reg_ret.location;
                                                                str_emv = _repPIX.GeraStringQRCODE(dtt_trans_pix.Rows[i]);
                                                                //str_ret = GeraImagemQRCODE(str_ret);
                                                                str_ret = "Transação PIX registrada.";
                                                            }
                                                            else
                                                            {
                                                                str_ret = "Transação PIX não registrada.";
                                                            }

                                                            dtt_trans_grv = _repPIX.CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
                                                            row = dtt_trans_grv.NewRow();
                                                            row["id"] = dtt_trans_pix.Rows[i]["id"];
                                                            row["id_chavepix"] = dtt_trans_pix.Rows[i]["id_chavepix"];
                                                            row["str_txid"] = dtt_trans_pix.Rows[i]["str_txid"];
                                                            row["int_expiracao"] = dtt_trans_pix.Rows[i]["int_expiracao"];
                                                            row["int_cpf_dev"] = dtt_trans_pix.Rows[i]["int_cpf_dev"];
                                                            row["int_cnpj_dev"] = dtt_trans_pix.Rows[i]["int_cnpj_dev"];
                                                            row["str_nome_dev"] = dtt_trans_pix.Rows[i]["str_nome_dev"];
                                                            row["dbl_valor_orig"] = dtt_trans_pix.Rows[i]["dbl_valor_orig"];
                                                            row["str_msg_devedor"] = dtt_trans_pix.Rows[i]["str_msg_devedor"];
                                                            row["str_data_cria"] = reg_ret.calendario.criacao;
                                                            row["int_revisao"] = reg_ret.revisao;
                                                            row["str_location"] = reg_ret.location;
                                                            row["int_cpf_pag"] = 0;
                                                            row["int_cnpj_pag"] = 0;
                                                            row["str_nome_pag"] = "";
                                                            row["str_msg_pagador"] = "";
                                                            row["str_id_devol"] = 0;
                                                            row["str_rtrid_devol"] = 0;
                                                            row["dbl_valor_devol"] = 0;
                                                            row["dtm_hora_sol_devol"] = "";
                                                            row["dtm_hora_liq_devol"] = "";
                                                            row["int_sit_devol"] = 0;
                                                            row["str_idfim"] = "";
                                                            row["int_filial"] = dtt_trans_pix.Rows[i]["int_filial"];
                                                            row["int_tipoped"] = dtt_trans_pix.Rows[i]["int_tipoped"];
                                                            row["int_pedido"] = dtt_trans_pix.Rows[i]["int_pedido"];
                                                            row["int_operador"] = dtt_trans_pix.Rows[i]["int_operador"];
                                                            row["int_caixa"] = dtt_trans_pix.Rows[i]["int_caixa"];
                                                            row["str_emv"] = str_emv;
                                                            row["int_situacao"] = (reg_ret.status == "ATIVA" ? 1 : (reg_ret.status == "CONCLUIDA" ? 2 : (reg_ret.status == "REMOVIDA_PELO_USUARIO_RECEBEDOR" ? 3 : 4)));
                                                            row["int_usu_lib"] = DBNull.Value;
                                                            row["int_usu_dev"] = DBNull.Value;

                                                            dtt_trans_grv.Rows.Add(row);

                                                            stbTran = _repPIX.SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                            str_ret = _repPIX.ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                            _intExpire = reg_ret.calendario.expiracao;
                                                            //_dtmInicioToken = Convert.ToDateTime(reg_ret.calendario.criacao);

                                                        }
                                                    }
                                                    else
                                                    {

                                                        if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                                                        {
                                                            str_ret = response.Content.ReadAsStringAsync().Result;
                                                            str_retorno = str_ret;

                                                        }
                                                        else
                                                        {
                                                            str_ret = response.Content.ReadAsStringAsync().Result;
                                                            if (str_ret == null)
                                                            {
                                                                str_ret = "";
                                                            }
                                                            str_ret += " Código do erro : " + response.StatusCode.ToString() + " Mensagem: " + response.RequestMessage.ToString();
                                                            str_retorno = str_ret;
                                                        }
                                                    }
                                                }

                                            }
                                        }
                                    }
                                    else
                                    {
                                        str_ret = "Problemas na geração do Token (" + str_token + ")";
                                        _logger.LogInformation(DateTime.Now.ToString("G") + " -  " + str_ret);
                                    }
                                }
                            }
                            else
                            {
                                str_retorno = "Não existe titulo para ser registrado no banco";
                            }

                            if (str_retorno.Contains("sucesso") || str_retorno.Contains("ok"))
                            {
                                transaction.Commit();
                            }
                            else
                            {
                                transaction.Rollback();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- RegistraTituloPIX erro: " + ex.Message.ToString());
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- RegistraTituloPIX Inner exception: " + ex.InnerException.Message.ToString());
                            transaction.Rollback();
                            conn.Close();
                            throw ex;
                        }
                        return str_retorno;
                    }
                    conn.Close();
                }
            }
            else
            {
                str_retorno = "Os campos Filial, Pedido, Tipo são obrogatórios.Chamada fora do padrão";
            }

            return str_retorno;
        }

        public string RevisaPIX(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_token = "";
            string str_msg = "";
            string str_id = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();
            List<Params> filtros = new List<Params>();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        filtros.AddRange(new List<Params>
                        {
                            new Params { nome = "id", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "id_empresa", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "cnpj", valor = "0", tipo = typeof(Int64).Name },
                            new Params { nome = "banco", valor = "0", tipo = typeof(Int32).Name },
                            new Params { nome = "chave_pix", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "str_txid", valor = "", tipo = typeof(string).Name },
                            new Params { nome = "int_codfil", valor = pedido.Filial.ToString(), tipo = typeof(Int32).Name },
                            new Params { nome = "str_tipoped", valor = pedido.Tipo.ToString(), tipo = typeof(string).Name },
                            new Params { nome = "int_pedido", valor = pedido.numpedido.ToString(), tipo = typeof(Int64).Name },
                            new Params { nome = "int_operador", valor = "-1", tipo = typeof(Int32).Name },
                            new Params { nome = "int_caixa", valor = "-2", tipo = typeof(Int16).Name },
                            new Params { nome = "Itens", valor = "0", tipo = typeof(Int16).Name },
                            //new Params { nome = "situacao", valor = StatusPIX.Devolver.ToString(), tipo = typeof(Int16).Name },
                            new Params { nome = "situacao", valor = "1", tipo = typeof(Int16).Name },
                            new Params { nome = "DtIni", valor = "2001-01-01", tipo = typeof(DateTime).Name },
                            new Params { nome = "DtFim", valor = "2001-01-01", tipo = typeof(DateTime).Name }
                        });
                        dtt_trans_pix = _repData.ConsultaGenericaDtt(filtros, "ntv_p_sel_tbl_transacao_pix", conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Achou pedido (" + pedido.numpedido.ToString() + ")");

                            //Local homologação
                            //var cert = new X509Certificate2(@"C:\Jackson\Clientes\Guaibim\certificados\pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            var cert = new X509Certificate2(@"./pixitau.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                            {
                                str_msg = "Certificado PIX Itaú (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";

                                List<envEmail> envMail = new List<envEmail>();
                                envMail.Add(new envEmail
                                {
                                    id = 0,
                                    id_assunto = 0,
                                    str_remet = _remet,
                                    str_dest = _dest,
                                    str_copias = _cc,
                                    str_copias_oc = _cco,
                                    str_corpo = str_msg,
                                    str_html = "N",
                                    str_anexo = "",
                                    dtm_inclusao = DateTime.Now.AddHours(-3),
                                    int_usuario = 696,
                                    str_erro = "",
                                    int_situacao = -1, //0 - Não enviado / 1 - Enviado / 2 - Com erro / -1 - Uma vez ao dia / -2 - Uma vez por semana / -3 - Uma vez por mês
                                    str_assunto = "Aviso de expiração de certificado - PIX"
                                });

                                str_retorno = ManutencaoTabela<envEmail>("I", envMail, "ntv_tbl_envio_emails", conn, null);

                                //str_ret = _repPIX.EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                            }
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Selecionou certificado " + cert.ToString());


                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(cert);

                            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                            handler.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                            handler.ServerCertificateCustomValidationCallback +=
                                (httpRequestMessage, cert2, cetChain, policyErrors) =>
                                {
                                    return policyErrors == SslPolicyErrors.None;
                                };

                            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));

                                var dict = new Dictionary<string, string>();
                                dict.Add("grant_type", "client_credentials");
                                dict.Add("client_id", dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString());
                                dict.Add("client_secret", dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();
                                /*
                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                //Dados do token inicio (08/11/2021 17:00:05) validade (3600) tempo decorrido (-88.23:59:54.2342864)
                                if (Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString()) < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                    respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                else
                                {
                                    token = new Token
                                    {
                                        //access_token = GetAppSetting("PixVariables:token"),
                                        access_token = obj_config["PixVariables"]["token"].ToString(),
                                        token_type = "Bearer",
                                        expires_in = Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString())
                                    };
                                    str_token = JsonConvert.SerializeObject(token);
                                }*/
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                respToken = client.PostAsync("https://sts.itau.com.br/api/oauth/token/", fencode).Result; //Produção
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                str_token = respToken.Content.ReadAsStringAsync().Result;
                                _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");


                                if (str_token.Contains("access_token"))
                                {
                                    if (respToken.StatusCode == HttpStatusCode.OK)
                                    {

                                        token = JsonConvert.DeserializeObject<Token>(str_token);
                                        _intExpire = token.expires_in;

                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                        //AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);

                                        if (token.access_token.Length > 0)
                                        {

                                            for (int i = 0; i < dtt_trans_pix.Rows.Count; i++)
                                            {
                                                // Associar o token aos headers do objeto
                                                // do tipo HttpClient
                                                client.DefaultRequestHeaders.Accept.Clear();
                                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                                str_json.Remove(0, str_json.Length);
                                                str_json.Append("{");
                                                str_json.Append("\"status\"" + ":" + "\"REMOVIDA_PELO_USUARIO_RECEBEDOR\"");
                                                str_json.Append("}");

                                                //Envia om para o banco
                                                //Produção
                                                HttpResponseMessage response = client.PatchAsync(dtt_trans_pix.Rows[0]["str_urlreg_pix"].ToString() + "/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;
                                                //Novo endpoint
                                                //HttpResponseMessage response = client.PatchAsync("https://pix-pj.api.itau.com/regulatorio-pix/v2" + "/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;                                                



                                                if (response.StatusCode == HttpStatusCode.OK)
                                                {
                                                    str_ret = response.Content.ReadAsStringAsync().Result;
                                                    if (str_ret.Length > 0)
                                                    {
                                                        DevQRCODE_237 reg_ret = JsonConvert.DeserializeObject<DevQRCODE_237>(str_ret);
                                                        DataRow row = null;

                                                        using (SqlTransaction transaction = conn.BeginTransaction())
                                                        {
                                                            try
                                                            {
                                                                dtt_trans_grv = _repData.CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
                                                                row = dtt_trans_grv.NewRow();
                                                                row["id"] = dtt_trans_pix.Rows[i]["id"];
                                                                row["id_chavepix"] = dtt_trans_pix.Rows[i]["id_chavepix"];
                                                                row["str_txid"] = dtt_trans_pix.Rows[i]["str_txid"];
                                                                row["int_expiracao"] = dtt_trans_pix.Rows[i]["int_expiracao"];
                                                                row["int_cpf_dev"] = dtt_trans_pix.Rows[i]["int_cpf_dev"];
                                                                row["int_cnpj_dev"] = dtt_trans_pix.Rows[i]["int_cnpj_dev"];
                                                                row["str_nome_dev"] = dtt_trans_pix.Rows[i]["str_nome_dev"];
                                                                row["dbl_valor_orig"] = dtt_trans_pix.Rows[i]["dbl_valor_orig"];
                                                                row["str_msg_devedor"] = dtt_trans_pix.Rows[i]["str_msg_devedor"];
                                                                row["str_data_cria"] = Convert.ToDateTime(dtt_trans_pix.Rows[i]["str_data_cria"]).ToString("yyyy-MM-dd hh:mm:ss");
                                                                row["int_revisao"] = dtt_trans_pix.Rows[i]["int_revisao"];
                                                                row["str_location"] = dtt_trans_pix.Rows[i]["str_location"];
                                                                row["int_cpf_pag"] = dtt_trans_pix.Rows[i]["int_cpf_pag"];
                                                                row["int_cnpj_pag"] = dtt_trans_pix.Rows[i]["int_cnpj_pag"];
                                                                row["str_nome_pag"] = dtt_trans_pix.Rows[i]["str_nome_pag"];
                                                                row["str_msg_pagador"] = dtt_trans_pix.Rows[i]["str_msg_pagador"];
                                                                row["str_id_devol"] = str_id;
                                                                row["str_rtrid_devol"] = reg_ret.rtrid;
                                                                row["dbl_valor_devol"] = reg_ret.valor;
                                                                row["dtm_hora_sol_devol"] = reg_ret.horario.solicitacao;
                                                                row["dtm_hora_liq_devol"] = reg_ret.horario.liquidacao;
                                                                row["int_sit_devol"] = (reg_ret.status == "EM_PROCESSAMENTO" ? 0 : (reg_ret.status == "REMOVIDA_PELO_USUARIO_RECEBEDOR" ? 9 : 2));
                                                                row["str_idfim"] = dtt_trans_pix.Rows[i]["str_idfim"];
                                                                row["int_filial"] = dtt_trans_pix.Rows[i]["int_filial"];
                                                                row["int_tipoped"] = dtt_trans_pix.Rows[i]["int_tipoped"];
                                                                row["int_pedido"] = dtt_trans_pix.Rows[i]["int_pedido"];
                                                                row["int_operador"] = dtt_trans_pix.Rows[i]["int_operador"];
                                                                row["int_caixa"] = dtt_trans_pix.Rows[i]["int_caixa"];
                                                                row["str_emv"] = dtt_trans_pix.Rows[i]["str_emv"];
                                                                row["int_situacao"] = StatusPIX.Devolvido;
                                                                row["int_usu_lib"] = DBNull.Value;
                                                                row["int_usu_dev"] = DBNull.Value;

                                                                dtt_trans_grv.Rows.Add(row);

                                                                stbTran = _repData.SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = _repData.ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                transaction.Commit();
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- RevisaPIX  erro: " + ex.Message.ToString());
                                                                transaction.Rollback();
                                                                throw ex;
                                                            }
                                                        }

                                                        str_ret = "Alteração da Transação PIX efetuada com sucesso.";
                                                    }
                                                }
                                                else
                                                {

                                                    if (response.StatusCode == HttpStatusCode.PreconditionFailed)
                                                    {
                                                        str_ret = response.Content.ReadAsStringAsync().Result;
                                                        str_retorno = str_ret;

                                                    }
                                                    else
                                                    {
                                                        str_ret = "Código do erro : " + response.StatusCode.ToString() + " Mensagem: " + response.RequestMessage.ToString();
                                                        str_retorno = str_ret;
                                                    }
                                                }
                                            }

                                        }
                                    }
                                }
                                else
                                {
                                    str_ret = "Problemas na geração do Token.";
                                }

                            }
                        }
                        else
                        {
                            str_retorno = "Não existe pedido para ser registrado no banco";
                        }

                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- RevisaPIX  erro 2: " + ex.Message.ToString());
                        conn.Close();
                        throw ex;
                    }

                    conn.Close();
                    return str_retorno;
                }
            }
            else
            {
                str_retorno = "Os campos Filial, Pedido, Tipo são obrogatórios.Chamada fora do padrão";
            }

            return str_retorno;
        }

        public string SendCSRPIX()
        {
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string client_id_cif = "7b3f0e56-a5cc-43dd-99b1-c2f3eeb761e3";
            string token_cif = "eyJraWQiOiIxNDZlNTY1Yy02ZjQ4LTRhN2EtOTU3NS1kYjg2MjE5YTc5N2MucHJkLmdlbi4xNTk3NjAwMTI1ODQ4Lmp3dCIsImFsZyI6IlJTMjU2In0.eyJzdWIiOiI3YjNmMGU1Ni1hNWNjLTQzZGQtOTliMS1jMmYzZWViNzYxZTMiLCJpc3MiOiJodHRwczovL29wZW5pZC5pdGF1LmNvbS";
            string str_token = "";


            _logger.LogInformation(DateTime.Now.ToString("G") + " PIXItau -- Iniciou");

            var handler = new HttpClientHandler();
            HttpResponseMessage respToken = new HttpResponseMessage();

            using (var client = new HttpClient(handler))
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token_cif);

                StringContent strCont = new StringContent("MIICuDCCAaACAQAwczEtMCsGA1UEAwwkN2IzZjBlNTYtYTVjYy00M2RkLTk5YjEtYzJmM2VlYjc2MWUzMRYwFAYDVQQLDA1OYXRpdmlkYWRlUElYMRAwDgYDVQQHDAdHVUFSVUpBMQswCQYDVQQIDAJTUDELMAkGA1UEBhMCQlIwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQC5iWWjgKsWahyHKpbEMuq7KJYWMXji0z2qaX9QqXYn0wA53u3MSx4 / pTmg / a + RG2PYwMITdmrXtyXKbXnkUvTfyrSFZHVJ5qZGD6XYE1zzUr0sroeZxXXixfqkEESWpf / D5LuHMRnlYmH16M92TSEGYVARyfvgChziKtRLbPTRdYxCSX6uwFdvk + QXWYhxioHbiVdhjHfi / BuiDzS88dgxogdt + q6nOUr / 5LRIMnmozWW383pT9WXVIj8isFjReAyMwqXbSWlaiYOfqQzqe3p9x + tBZznF73HHB + GsfT9aZI03J + 23eEm4hJ9f3 / 8KWo +/ 9YDvK + aPYwKGVONsj + FFAgMBAAGgADANBgkqhkiG9w0BAQ0FAAOCAQEAE7x996qiZR22kI2MzYiOp0d2H2//P0cZ+lWd56JyX6jFI348R9iKzFmCUdmiNoFpChjGWuwUiR4t96e7 / RXXAMEGANLDDWfiYSK5ka4QL5wRoCuwzliTXPyUfLrUeSMff7GwkD9rq6Tll6cBuFld7ByUlXa / ZrysFYg2GQHBxRqz0HXDnPd16nzP9m2UtT0N5vm2OPQhdYQsWBpJNk3EU2FTu / jSLbX2UlCuCpznep6BN4KYAnx7dAAXIabKp6w6e / WnuiDt + 2amkC + Bzxq37btNO3t + QNhM92A / KPPVT7HesqGWB0DGX4l0G1fsRKCAnR92SpYZD27H / hC2fc62BQ == ");
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token ");
                respToken = client.PostAsync("https://sts.itau.com.br/seguranca/v1/certificado/solicitacao", strCont).Result; //Homologação
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                str_token = respToken.Content.ReadAsStringAsync().Result;
                _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            return str_token;

        }

        private static void AddOrUpdateJSON<T>(string arq, string key, T value)
        {
            try
            {

                var filePath = Path.Combine(arq);
                string json = File.ReadAllText(filePath);
                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                var sectionPath = key.Split(":")[0];
                if (!string.IsNullOrEmpty(sectionPath))
                {
                    var keyPath = key.Split(":")[1];
                    jsonObj[sectionPath][keyPath] = value;
                }
                else
                {
                    jsonObj[sectionPath] = value; // if no sectionpath just set the value
                }
                string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(filePath, output);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private byte[] GetBytesFromPEM(string pemString, string section)
        {
            var header = String.Format("-----BEGIN PRIVATE KEY-----", section);
            var footer = String.Format("-----END PRIVATE KEY-----", section);

            var start = pemString.IndexOf(header, StringComparison.Ordinal);
            if (start < 0)
                return null;

            start += header.Length;
            var end = pemString.IndexOf(footer, start, StringComparison.Ordinal) - start;

            if (end < 0)
                return null;

            return Convert.FromBase64String(pemString.Substring(start, end));
        }


        public string ManutencaoTabela<T>(string Operacao, IList<T> dados, string Tabela, SqlConnection conn, SqlTransaction tran)
        {
            _logger.LogInformation(DateTime.Now.ToString("G") + " ManutencaoTabela - Dados list (" + dados.ToString() + ")");
            DataTable dtt_dados = ToDataTable<T>(dados);
            _logger.LogInformation(DateTime.Now.ToString("G") + " ManutencaoTabela - Dados datatable (" + dtt_dados.ToString() + ")");
            string str_dadosXml = SaveThroughXML(dtt_dados, Tabela).ToString();
            _logger.LogInformation(DateTime.Now.ToString("G") + " ManutencaoTabela - Dados XML (" + str_dadosXml + ")");

            try
            {
                SqlCommand command = conn.CreateCommand();
                SqlParameter str_retorno = new SqlParameter();
                str_retorno.Direction = ParameterDirection.Output;
                str_retorno.Size = 1000;
                str_retorno.ParameterName = "@Novos_id";
                str_retorno.SqlDbType = SqlDbType.VarChar;

                if (tran != null)
                {
                    command.Transaction = tran;
                }
                command.CommandText = "ntv_p_man_" + Tabela.Replace("ntv_", "");
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@Tp_processo", Operacao));
                command.Parameters.Add(new SqlParameter("@Dados", str_dadosXml));
                command.Parameters.Add(str_retorno);

                _logger.LogInformation(DateTime.Now.ToString("G") + " ManutencaoTabela - Antes de enviar ao banco");
                command.ExecuteNonQuery();
                _logger.LogInformation(DateTime.Now.ToString("G") + " ManutencaoTabela - Deposi de enviar ao banco");

                return str_retorno.Value.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogInformation(DateTime.Now.ToString("G") + " ManutencaoTabela erro - " + ex.Message);
                throw ex;
            }
        }

        private static DataTable ToDataTable<C>(IList<C> data)
        {
            DataTable table = new DataTable();
            DataColumn dc = null;
            string[] types = new string[] { "Int64", "Int32", "Int16", "int", "DateTime", "string", "double" };
            //static Dictionary<string, string> dict = new Dictionary<string, string>();

            try
            {
                PropertyDescriptorCollection props =
                    TypeDescriptor.GetProperties(data[0]);

                for (int i = 0; i < props.Count; i++)
                {
                    dc = new DataColumn();

                    PropertyDescriptor prop = props[i];
                    dc.ColumnName = prop.Name;
                    switch (prop.PropertyType.ToString())
                    {
                        case var s when prop.PropertyType.ToString().Contains("int"):
                            dc.DataType = typeof(int);
                            break;

                        case var s when prop.PropertyType.ToString().Contains("Int16"):
                            dc.DataType = typeof(Int16);
                            break;

                        case var s when prop.PropertyType.ToString().Contains("Int32"):
                            dc.DataType = typeof(Int32);
                            break;

                        case var s when prop.PropertyType.ToString().Contains("Int64"):
                            dc.DataType = typeof(Int64);
                            break;

                        case var s when prop.PropertyType.ToString().Contains("String"):
                            dc.DataType = typeof(string);
                            break;

                        case var s when prop.PropertyType.ToString().Contains("DateTime"):
                            dc.DataType = typeof(DateTime);
                            break;

                        case var s when prop.PropertyType.ToString().Contains("string"):
                            dc.DataType = typeof(string);
                            break;

                        case var s when prop.PropertyType.ToString().Contains("double"):
                            dc.DataType = typeof(double);
                            break;
                    }
                    //string sKeyResult = types.FirstOrDefault<string>(s => prop.PropertyType.ToString().Contains(s));


                    //table.Columns.Add(prop.Name, prop.PropertyType);
                    table.Columns.Add(dc);
                }
                object[] values = new object[props.Count];
                foreach (C item in data)
                {
                    for (int i = 0; i < values.Length; i++)
                    {
                        if (props[i].PropertyType.ToString().Contains("DateTime"))
                        {
                            //values[i] = (props[i].GetValue(item) == null ? props[i].GetValue(item) : Convert.ToDateTime(props[i].GetValue(item)).ToString("dd/MM/yyyy HH:mm:ss"));
                            values[i] = (props[i].GetValue(item) == null ? props[i].GetValue(item) : Convert.ToDateTime(props[i].GetValue(item)).ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                        else
                        {
                            if (props[i].PropertyType.ToString().Contains("double") ||
                                props[i].PropertyType.ToString().Contains("Double"))
                            {
                                values[i] = Convert.ToDouble(props[i].GetValue(item)).ToString().Replace(",", ".");
                            }
                            else
                            {
                                values[i] = props[i].GetValue(item);
                            }
                        }

                    }
                    table.Rows.Add(values);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return table;

        }

        public StringBuilder SaveThroughXML(DataTable DtTabela, string NomeTabela)
        {
            StringBuilder sb = new System.Text.StringBuilder(1000);
            StringWriter sw = new System.IO.StringWriter(sb);

            DtTabela.TableName = NomeTabela;
            foreach (DataColumn col in DtTabela.Columns)
            {
                col.ColumnMapping = System.Data.MappingType.Attribute;
            }

            DtTabela.WriteXml(sw, System.Data.XmlWriteMode.WriteSchema);
            return sb;
        }

        public string ConsultaDevolucaoPIX(Pedido pedido)
        {
            throw new NotImplementedException();
        }
    }
}
