using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PIX.Models;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace PIX.Repositories
{    

    public enum StatusPIX
    {
        Todos = -1,
        Remover = -3,
        Devolver = -9,
        Aguardando_registro = 0,
        Ativa =  1,
        Concluída = 2,
        Removida_recebedor = 3,
        Removida_banco = 4,
        Devolvido = 9
    }

    public enum StatusPIXDev
    {
        Em_processamento = 0,
        Devolvido = 1,
        Nao_realizado = 2
    }

    public class PIXRepository
    {

        private readonly string _strConnect;
        private readonly IConfiguration _config;
        private readonly string _webhookUrl;
        private readonly string _webhookUrlHomologa;
        private CriptografiaNtv criptNtv = new CriptografiaNtv();
        private Int64 _intExpire;
        private string _dtmInicioToken;
        private Token token = new Token();
        private readonly ILogger _logger;
        private readonly ConfigEmail _configEmail;
        private readonly string _path;
        //private JObject obj_config;

        public PIXRepository(IConfiguration config, ILogger<PIXRepository> log)
        {
            _strConnect = config.GetConnectionString("DeafultConnectionStrings") + "@DTILGCF06FW";
            _webhookUrl = config.GetSection("webhook")["url"].ToString();
            _webhookUrlHomologa = config.GetSection("webhook")["urlHomologa"].ToString();
            _config = config;
            _logger = log;
            _configEmail = new ConfigEmail();
            config.GetSection("DadosEmail").Bind(_configEmail);
            /*_path = Path.Combine(".\\ConfigPIX.json");
            var JSON = System.IO.File.ReadAllText(_path);
            if (JSON != null && JSON.ToString() != "") {
                obj_config = JObject.Parse(JSON);
            }
            else {
                obj_config = JObject.Parse("{\"PixVariables\": {\"expire\": 360,\"token\": \"e\",\"inicio\": \"2001-01-01 00:00:02\"}}");
            }*/

        }

        public string ProcessaPIXDistancia(Pedido pedido)
        {
            string str_retorno = "ok";
            if (pedido.Filial > 0 && pedido.Tipo > -1 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            ReplicaPedido(pedido.Filial, pedido.Tipo, pedido.numpedido, conn, transaction);
                            //FaturaPedido(pedido.Filial, pedido.Tipo, pedido.numpedido, conn, transaction);
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            conn.Close();
                            throw ex;
                        }
                        transaction.Commit();
                    }
                    conn.Close();
                }

            }
            return str_retorno;
        }

        public string RevisaQrcodeErpBradesco(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_itens = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();

            if (pedido.Filial > 0 && pedido.Tipo > -1 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            dtt_trans_pix = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Ativa, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn, transaction);

                            if (dtt_trans_pix.Rows.Count > 0)
                            {
                                //Produção
                                var cert = new X509Certificate2(@"C:\Jackson\Clientes\Guaibim\certificados\13850516000177.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                                //var cert = new X509Certificate2(@".\13850516000177.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                                //Homologação
                                //var cert = new X509Certificate2(@"C:\Jackson\Clientes\Guaibim\certificados\13850516000924.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                                //var cert = new X509Certificate2(@".\13850516000924.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                                var handler = new HttpClientHandler();
                                handler.ClientCertificates.Add(cert);

                                using (var client = new HttpClient(handler))
                                {

                                    //Busca autorização
                                    //str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + criptNtv.Descriptografar(dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                    str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_trans_pix.Rows[0]["str_senha_bco"].ToString();
                                    encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                                    encodedTxt = Convert.ToBase64String(encodedBytes);

                                    str_json.Remove(0, str_json.Length);
                                    client.DefaultRequestHeaders.Accept.Clear();
                                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                                    var dict = new Dictionary<string, string>();
                                    dict.Add("grant_type", "client_credentials");
                                    //Bradesco
                                    dict.Add("scope", "cob.read cob.write pix.read pix.write webhook.read webhook.write");
                                    //Brasil
                                    //dict.Add("scope", "pix.read pix.write cob.read cob.write");
                                    FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                    //_dtmInicioToken = GetAppSetting("PixVariables:inicio");
                                    //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();

                                    TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                    //Dados do token inicio (08/11/2021 17:00:05) validade (3600) tempo decorrido (-88.23:59:54.2342864)
                                    if (Convert.ToInt64(_dtmInicioToken) < Convert.ToInt64(tempo.TotalSeconds))
                                    {
                                        //Bradesco
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                        respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", fencode).Result;
                                        //respToken = client.PostAsync("https://qrpix.bradesco.com.br/auth/server/oauth/token", fencode).Result;
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                        //Brasil
                                        //respToken = client.PostAsync("https://oauth.hm.bb.com.br/oauth/token", fencode).Result;
                                        str_token = respToken.Content.ReadAsStringAsync().Result;
                                        _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                    /*else
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
                                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Monta JSON de envio ");
                                                    client.DefaultRequestHeaders.Accept.Clear();
                                                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                                    str_json.Remove(0, str_json.Length);
                                                    str_json.Append("{");
                                                    str_json.Append("\"tx_id\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_txid"].ToString() + "\",");
                                                    str_json.Append("\"status\"" + ":" + "\"REMOVIDA_PELO_USUARIO_RECEBEDOR\",");
                                                    /*str_json.Append("\"calendario\"" + ":{");
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
                                                    */

                                                    str_json.Append("\"valor\"" + ":{");
                                                    str_json.Append("\"original\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["dbl_valor_orig"].ToString().Replace(",", ".") + "\"");
                                                    str_json.Append("}");

                                                    if (i + 1 == dtt_trans_pix.Rows.Count)
                                                    {
                                                        str_json.Append("}");
                                                    }
                                                    else
                                                    {
                                                        str_json.Append("},");
                                                    }

                                                    //Envia om para o banco
                                                    //Bradesco
                                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Vai enviar pedido " + str_json.ToString());

                                                    //Homologação
                                                    HttpResponseMessage response = client.PatchAsync("https://qrpix-h.bradesco.com.br/cob/", new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;

                                                    //Produção
                                                    //HttpResponseMessage response = client.PatchAsync("https://qrpix.bradesco.com.br/cob/", new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;


                                                    //Brasil
                                                    //HttpResponseMessage response = client.PutAsync("https://api.hm.bb.com.br/pix/v1/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString() + "?gw-dev-app-key=" + dtt_trans_pix.Rows[i]["str_key_app"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;

                                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Retorno do envio " + response.ToString());

                                                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                                                    {
                                                        str_ret = response.Content.ReadAsStringAsync().Result;
                                                        if (str_ret.Length > 0)
                                                        {
                                                            RegQRCODE_237 reg_ret = JsonConvert.DeserializeObject<RegQRCODE_237>(str_ret);
                                                            DataRow row = null;

                                                            if (reg_ret.status == "REMOVIDA_PELO_USUARIO_RECEBEDOR")
                                                            {

                                                                dtt_trans_grv = CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
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
                                                                row["str_emv"] = dtt_trans_pix.Rows[i]["str_emv"];
                                                                row["int_situacao"] = 3;
                                                                row["int_usu_lib"] = DBNull.Value;
                                                                row["int_usu_dev"] = DBNull.Value;

                                                                dtt_trans_grv.Rows.Add(row);

                                                                stbTran = SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                _intExpire = reg_ret.calendario.expiracao;
                                                            }
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

                            if (str_retorno.Contains("sucesso"))
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
                            transaction.Rollback();
                            conn.Close();
                            throw ex;
                        }

                        conn.Close();
                        return str_retorno;
                    }
                    
                }
            }
            else
            {
                str_retorno = "Os campos Filial, Pedido, Tipo são obrogatórios.Chamada fora do padrão";
            }

            return str_retorno;
        }

        public string RegistraQrcodeErp(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";
            string str_emv = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_itens = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            dtt_trans_pix = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Aguardando_registro, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn, transaction);

                            if (dtt_trans_pix.Rows.Count > 0)
                            {
                                var cert = new X509Certificate2();
                                cert = SelecionaCertificado(dtt_trans_pix.Rows[0]["int_cnpj"].ToString(), "1234");

                                var handler = new HttpClientHandler();

                                using (var client = new HttpClient(handler))
                                {

                                    //Busca autorização
                                    //str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + criptNtv.Descriptografar(dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                    str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_trans_pix.Rows[0]["str_senha_bco"].ToString();
                                    encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                                    encodedTxt = Convert.ToBase64String(encodedBytes);

                                    str_json.Remove(0, str_json.Length);
                                    client.DefaultRequestHeaders.Accept.Clear();
                                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                                    /*str_json.Append("{");
                                    str_json.Append("\"grant_type\"" + ":" + "\"client_credentials\",");
                                    str_json.Append("\"scope\":\"cob.write cob.read\" ");
                                    str_json.Append("}");*/

                                    var dict = new Dictionary<string, string>();
                                    dict.Add("grant_type", "client_credentials");
                                    //Bradesco
                                    //dict.Add("scope", "cob.write cob.read");
                                    //Brasil
                                    dict.Add("scope", "pix.read pix.write cob.read cob.write");
                                    FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                    TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                    if (token.expires_in < Convert.ToInt64(tempo.TotalSeconds))
                                    {
                                        //Brasil
                                        respToken = client.PostAsync("https://oauth.hm.bb.com.br/oauth/token", fencode).Result;
                                        str_token = respToken.Content.ReadAsStringAsync().Result;
                                    }
                                    /*else
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
                                            _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                            _intExpire = token.expires_in;

                                            /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                            AddOrUpdateAppSetting("PixVariables:token", token.access_token);*/
                                            AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                            AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);

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
                                                    //str_json.Append("\"tx_id\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_txid"].ToString() + "\",");

                                                    str_json.Append("\"calendario\"" + ":{");
                                                    str_json.Append("\"expiracao\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["int_expiracao"].ToString() + "\"");
                                                    str_json.Append("},");

                                                    str_json.Append("\"devedor\"" + ":{");
                                                    if (Convert.ToInt64(dtt_trans_pix.Rows[0]["int_cpf_dev"].ToString()) > 0)
                                                    {
                                                        str_json.Append("\"cpf\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["int_cpf_dev"].ToString() + "\",");
                                                    }
                                                    else
                                                    {
                                                        str_json.Append("\"cnpj\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["int_cnpj_dev"].ToString() + "\",");
                                                    }                                                    
                                                    str_json.Append("\"nome\"" + ":" + "\"" + (dtt_trans_pix.Rows[0]["str_nome_dev"].ToString().Length > 0 ? dtt_trans_pix.Rows[0]["str_nome_dev"].ToString() : "Jackson Vago") + "\"");
                                                    str_json.Append("},");

                                                    str_json.Append("\"valor\"" + ":{");
                                                    str_json.Append("\"original\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["dbl_valor_orig"].ToString().Replace(",",".") + "\"");
                                                    str_json.Append("},");

                                                    //str_json.Append("\"chave\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_chavepix"].ToString() + "\",");
                                                    str_json.Append("\"chave\"" + ":" + "\"" + "7f6844d0-de89-47e5-9ef7-e0a35a681615" + "\",");
                                                    
                                                    str_json.Append("\"solicitacaopagador\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["str_msg_devedor"].ToString() + "\"");

                                                    dtt_trans_itens = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Aguardando_registro, 1, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn, transaction);

                                                    if (dtt_trans_itens.Rows.Count > 0)
                                                    {
                                                        str_json.Append("\"info_adicionais\"" + ":[");
                                                        for (int linf = 0; linf < dtt_trans_itens.Rows.Count; linf++)
                                                        {
                                                            str_json.Append("\"info_adicionais\"" + "{");
                                                            str_json.Append("\"info_adicionais.nome\"\",");
                                                            str_json.Append("\"info_adicionais.valor\"\"");

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
                                                    //Brasil
                                                    HttpResponseMessage response = client.PutAsync("https://api.hm.bb.com.br/pix/v1/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString() + "?gw-dev-app-key=" + dtt_trans_pix.Rows[i]["str_chave_app"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;                                                

                                                    if (response.StatusCode == HttpStatusCode.OK)
                                                    {
                                                        str_ret = response.Content.ReadAsStringAsync().Result;
                                                        if (str_ret.Length > 0)
                                                        {
                                                            RegQRCODE_237 reg_ret = JsonConvert.DeserializeObject<RegQRCODE_237>(str_ret);
                                                            DataRow row = null;

                                                            if (reg_ret.status == "ATIVA")
                                                            {
                                                                dtt_trans_pix.Rows[i]["str_location"] = reg_ret.location;
                                                                str_emv = GeraStringQRCODE(dtt_trans_pix.Rows[0]);
                                                                //str_ret = GeraImagemQRCODE(str_ret);
                                                                str_ret = "Transação PIX registrada.";
                                                            }
                                                            else
                                                            {
                                                                str_ret = "Transação PIX não registrada.";
                                                            }

                                                            dtt_trans_grv = CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
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

                                                            stbTran = SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                            str_ret = ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                            _intExpire = reg_ret.calendario.expiracao;
                                                            //_dtmInicioToken = Convert.ToDateTime(reg_ret.calendario.criacao);

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

        public string RegistraQrcodeErpBradesco(Pedido pedido)
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

            Int64 id_pix = 0;

            HttpResponseMessage respToken = new HttpResponseMessage();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                //Identifica se o pedido tem 2 PIX´s                
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Registra QR CODE pedido (" + pedido.numpedido.ToString() + ")");
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();

                    dtt_pixs = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Todos, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);

                    if (dtt_pixs.Rows.Count > 1) { 
                        for (int ped=0; ped < dtt_pixs.Rows.Count; ped++)
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
                        bol_registra = true;
                        id_pix = Convert.ToInt64(dtt_pixs.Rows[0]["id"]);
                    }
                    if (bol_registra)
                    {
                        using (SqlTransaction transaction = conn.BeginTransaction())
                        {
                            try
                            {

                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Consulta pedido (" + pedido.numpedido.ToString() + ")");
                                dtt_trans_pix = ConsultaTransacaoPIX(id_pix, 0, -2, conn, transaction);

                                if (dtt_trans_pix.Rows.Count > 0)
                                {
                                    //AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Achou pedido (" + pedido.numpedido.ToString() + ")");
                                    //Produção
                                    var cert = new X509Certificate2(".\\13850516000177.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                                    //Local producao
                                    //var cert = new X509Certificate2(@"C:\Jackson\Clientes\Guaibim\certificados\13850516000177.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                                    //Local homologação
                                    //var cert = new X509Certificate2(@"C:\Jackson\Clientes\Guaibim\certificados\13850516000924.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                                    //var cert = new X509Certificate2(@".\13850516000924.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                                    if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                                    {
                                        str_msg = "Certificado CNPJ (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";
                                        str_ret = EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                                    }
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Selecionou certificado " + cert.ToString());

                                    var handler = new HttpClientHandler();

                                    //Bradesco
                                    handler.ClientCertificates.Add(cert);

                                    using (var client = new HttpClient(handler))
                                    {

                                        //Busca autorização
                                        //str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + criptNtv.Descriptografar(dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                        str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_trans_pix.Rows[0]["str_senha_bco"].ToString();
                                        encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                                        encodedTxt = Convert.ToBase64String(encodedBytes);

                                        str_json.Remove(0, str_json.Length);
                                        client.DefaultRequestHeaders.Accept.Clear();
                                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                                        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                                        var dict = new Dictionary<string, string>();
                                        dict.Add("grant_type", "client_credentials");
                                        //Bradesco
                                        dict.Add("scope", "cob.read cob.write pix.read pix.write webhook.read webhook.write");
                                        //Brasil
                                        //dict.Add("scope", "pix.read pix.write cob.read cob.write");
                                        FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                        //_dtmInicioToken = GetAppSetting("PixVariables:inicio");
                                        //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();
                                        
                                        TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                        //Dados do token inicio (08/11/2021 17:00:05) validade (3600) tempo decorrido (-88.23:59:54.2342864)
                                        if (Convert.ToInt64(_dtmInicioToken) < Convert.ToInt64(tempo.TotalSeconds))
                                        {
                                            //Bradesco
                                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                            respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", fencode).Result;
                                            //respToken = client.PostAsync("https://qrpix.bradesco.com.br/auth/server/oauth/token", fencode).Result;
                                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                            //Brasil
                                            //respToken = client.PostAsync("https://oauth.hm.bb.com.br/oauth/token", fencode).Result;
                                            str_token = respToken.Content.ReadAsStringAsync().Result;
                                            _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                        }
                                        /*else
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

                                                /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                                AddOrUpdateAppSetting("PixVariables:token", token.access_token);
                                                AddOrUpdateAppSetting("PixVariables:inicio", _dtmInicioToken);*/
                                                AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                                AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                                AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);


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
                                                        //str_json.Append("\"tx_id\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_txid"].ToString() + "\",");

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

                                                        //str_json.Append("\"chave\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_chavepix"].ToString() + "\",");
                                                        str_json.Append("\"chave\"" + ":" + "\"" + "4581f4b7-957f-4aba-9ae8-c1c174e9452c" + "\",");

                                                        str_json.Append("\"solicitacaopagador\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["str_msg_devedor"].ToString() + "\"");

                                                        dtt_trans_itens = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Ativa, 1, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn, transaction);


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
                                                        //Bradesco
                                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Vai enviar pedido " + str_json.ToString());

                                                        //Homologação
                                                        HttpResponseMessage response = client.PutAsync("https://qrpix-h.bradesco.com.br/v1/spi/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;

                                                        //Produção
                                                        //HttpResponseMessage response = client.PutAsync("https://qrpix.bradesco.com.br/v1/spi/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;


                                                        //Brasil
                                                        //HttpResponseMessage response = client.PutAsync("https://api.hm.bb.com.br/pix/v1/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString() + "?gw-dev-app-key=" + dtt_trans_pix.Rows[i]["str_key_app"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;

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
                                                                    str_emv = GeraStringQRCODE(dtt_trans_pix.Rows[i]);
                                                                    //str_ret = GeraImagemQRCODE(str_ret);
                                                                    str_ret = "Transação PIX registrada.";
                                                                }
                                                                else
                                                                {
                                                                    str_ret = "Transação PIX não registrada.";
                                                                }

                                                                dtt_trans_grv = CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
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

                                                                stbTran = SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

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
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Ocorreu erro: " + ex.Message.ToString());
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Inner exceprion: " + ex.InnerException.Message.ToString());
                                transaction.Rollback();
                                conn.Close();
                                throw ex;
                            }
                            return str_retorno;
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
        

        public string RegistraQrcodeTitBradesco(Int64 id)
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

            Int64 id_pix = 0;

            HttpResponseMessage respToken = new HttpResponseMessage();

            if (id > 0 )
            {
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Registra QR CODE titulo (" + id.ToString() + ")");
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();

                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {

                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Consulta Titulo (" + id.ToString() + ")");
                            dtt_trans_pix = ConsultaTransacaoPIX(id, "9", 0, -2, conn, transaction);

                            if (dtt_trans_pix.Rows.Count > 0)
                            {
                                //AppContext.SetSwitch("System.Net.Http.UseSocketsHttpHandler", false);
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Achou pedido (" + id.ToString() + ")");
                                var cert = new X509Certificate2(".\\13850516000177.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                                //var cert = new X509Certificate2(@"C:\Jackson\Clientes\Guaibim\certificados\13850516000177.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                                if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                                {
                                    str_msg = "Certificado CNPJ (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";
                                    str_ret = EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                                }
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Selecionou certificado " + cert.ToString());

                                var handler = new HttpClientHandler();

                                //Bradesco
                                handler.ClientCertificates.Add(cert);

                                using (var client = new HttpClient(handler))
                                {

                                    //Busca autorização
                                    //str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + criptNtv.Descriptografar(dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                    str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_trans_pix.Rows[0]["str_senha_bco"].ToString();
                                    encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                                    encodedTxt = Convert.ToBase64String(encodedBytes);

                                    str_json.Remove(0, str_json.Length);
                                    client.DefaultRequestHeaders.Accept.Clear();
                                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                                    //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                                    var dict = new Dictionary<string, string>();
                                    dict.Add("grant_type", "client_credentials");
                                    //Bradesco
                                    dict.Add("scope", "cob.read cob.write pix.read pix.write webhook.read webhook.write");
                                    //Brasil
                                    //dict.Add("scope", "pix.read pix.write cob.read cob.write");
                                    FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                    //_dtmInicioToken = GetAppSetting("PixVariables:inicio");
                                    //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();

                                    TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                    //Dados do token inicio (08/11/2021 17:00:05) validade (3600) tempo decorrido (-88.23:59:54.2342864)
                                    if (token.expires_in < Convert.ToInt64(tempo.TotalSeconds))
                                    {
                                        //Bradesco
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- vai buscar token " + cert.ToString());
                                        //respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", fencode).Result;
                                        respToken = client.PostAsync("https://qrpix.bradesco.com.br/auth/server/oauth/token", fencode).Result;
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- retorno token " + respToken.ToString());
                                        //Brasil
                                        //respToken = client.PostAsync("https://oauth.hm.bb.com.br/oauth/token", fencode).Result;
                                        str_token = respToken.Content.ReadAsStringAsync().Result;
                                        _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                    }
                                    /*else
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

                                            /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                            AddOrUpdateAppSetting("PixVariables:token", token.access_token);
                                            AddOrUpdateAppSetting("PixVariables:inicio", _dtmInicioToken);*/
                                            AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                            AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                            AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);

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
                                                    //str_json.Append("\"tx_id\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_txid"].ToString() + "\",");

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

                                                    str_json.Append("\"chave\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_chavepix"].ToString() + "\",");
                                                    //str_json.Append("\"chave\"" + ":" + "\"" + "14a3a60b-d431-4150-a1fc-41c5ffa57a31" + "\",");

                                                    str_json.Append("\"solicitacaopagador\"" + ":" + "\"" + dtt_trans_pix.Rows[0]["str_msg_devedor"].ToString() + "\",");

                                                    dtt_trans_itens = ConsultaTransacaoPIX(1, "9", id, StatusPIX.Ativa, 1, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn, transaction);


                                                    if (dtt_trans_itens.Rows.Count > 0)
                                                    {
                                                        str_json.Append("\"info_adicionais\"" + ":[");
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
                                                    //Bradesco
                                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Vai enviar pedido " + str_json.ToString());
                                                    //HttpResponseMessage response = client.PutAsync("https://qrpix-h.bradesco.com.br/v1/spi/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;
                                                    HttpResponseMessage response = client.PutAsync("https://qrpix.bradesco.com.br/v1/spi/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;


                                                    //Brasil
                                                    //HttpResponseMessage response = client.PutAsync("https://api.hm.bb.com.br/pix/v1/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString() + "?gw-dev-app-key=" + dtt_trans_pix.Rows[i]["str_key_app"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;

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
                                                                str_emv = GeraStringQRCODE(dtt_trans_pix.Rows[i]);
                                                                //str_ret = GeraImagemQRCODE(str_ret);
                                                                str_ret = "Transação PIX registrada.";
                                                            }
                                                            else
                                                            {
                                                                str_ret = "Transação PIX não registrada.";
                                                            }

                                                            dtt_trans_grv = CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
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

                                                            stbTran = SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                            str_ret = ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

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
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Ocorreu erro: " + ex.Message.ToString());
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Inner exceprion: " + ex.InnerException.Message.ToString());
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

        public string ConsultaQRCODE(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        //dtt_trans_pix = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Ativa, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);
                        dtt_trans_pix = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, 0, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            var cert = new X509Certificate2();
                            cert = SelecionaCertificado(dtt_trans_pix.Rows[0]["int_cnpj"].ToString(), "1234");

                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(cert);

                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_trans_pix.Rows[0]["str_senha_bco"].ToString();
                                encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                                encodedTxt = Convert.ToBase64String(encodedBytes);

                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                                var dict = new Dictionary<string, string>();
                                //Brasil
                                dict.Add("grant_type", "client_credentials");
                                dict.Add("scope", "cob.read cob.write pix.read pix.write");
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                //token.expires_in = Convert.ToInt64(_config.GetSection("PixVariables")["expire"].ToString());
                                if (token.expires_in < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    //Brasil
                                    respToken = client.PostAsync("https://oauth.hm.bb.com.br/oauth/token", fencode).Result;
                                    str_token = respToken.Content.ReadAsStringAsync().Result;                                    
                                }
                                /*else
                                {
                                    token = new Token
                                    {
                                        //access_token = _config.GetSection("PixVariables")["token"].ToString(),
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
                                        _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                        _intExpire = token.expires_in;

                                        /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                        AddOrUpdateAppSetting("PixVariables:token", token.access_token);*/
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);

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
                                                HttpResponseMessage response = client.GetAsync("https://api.hm.bb.com.br/pix/v1/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString() + "?gw-dev-app-key=" + dtt_trans_pix.Rows[i]["str_chave_app"].ToString()).Result;

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
                                                                dtt_trans_grv = CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
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

                                                                stbTran = SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                transaction.Commit();
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                transaction.Rollback();
                                                                throw ex;
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
                            str_retorno = "Não existem pedidos pendente de pagamentos   ";
                        }

                    }
                    catch (Exception ex)
                    {
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

        public string ConsultaQRCODEBradesco(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";
            string str_emv = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        dtt_trans_pix = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Ativa, 0, Convert.ToDateTime("2001/01/01"), Convert.ToDateTime("2001/01/01"), -2, conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Vai buscar certificado .");
                            //Produção
                            var cert = new X509Certificate2(".\\13850516000177.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                            //Local Produção
                            //var cert = new X509Certificate2(@"C:\Jackson\Clientes\Guaibim\certificados\13850516000177.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                            //Local homologação
                            //var cert = new X509Certificate2(@"C:\Jackson\Clientes\Guaibim\certificados\13850516000924.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            //var cert = new X509Certificate2(@".\13850516000924.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(cert);

                            _logger.LogInformation(DateTime.Now.ToString("G") + "-- Adicionou certificado .");
                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Busca autorização .");
                                str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_trans_pix.Rows[0]["str_senha_bco"].ToString();
                                encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                                encodedTxt = Convert.ToBase64String(encodedBytes);

                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Monta Haeder .");
                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                                var dict = new Dictionary<string, string>();
                                dict.Add("grant_type", "client_credentials");
                                //Bradesco
                                dict.Add("scope", "cob.read cob.write pix.read pix.write webhook.read webhook.write");
                                //Brasil
                                //dict.Add("scope", "pix.read pix.write cob.read cob.write");
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                //_dtmInicioToken = GetAppSetting("PixVariables:inicio");
                                //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();

                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Dados do token inicio (" + _dtmInicioToken.ToString() + ") validade (" + token.expires_in.ToString() + ") tempo decorrido (" + tempo.TotalSeconds.ToString() + ")");
                                //token.expires_in = Convert.ToInt64(_config.GetSection("PixVariables")["expire"].ToString());
                                if (token.expires_in < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Token expirado pega novo .");
                                    //Bradesco
                                    //Homologação
                                    respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", fencode).Result;

                                    //Produção
                                    //respToken = client.PostAsync("https://qrpix.bradesco.com.br/auth/server/oauth/token", fencode).Result;

                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Retornou novo token .");
                                }
                                /*else
                                {
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Mantem token que não expirou .");
                                    token = new Token
                                    {
                                        //access_token = _config.GetSection("PixVariables")["token"].ToString(),
                                        //access_token = GetAppSetting("PixVariables:token"),
                                        access_token = obj_config["PixVariables"]["token"].ToString(),
                                        token_type = "Bearer",
                                        expires_in = Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString())
                                    };
                                    str_token = JsonConvert.SerializeObject(token);
                                }*/

                                if (str_token.Contains("access_token"))
                                {
                                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Status token :" + respToken.StatusCode.ToString());
                                    if (respToken.StatusCode == HttpStatusCode.OK)
                                    {
                                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Token OK ");
                                        token = JsonConvert.DeserializeObject<Token>(str_token);
                                        _intExpire = token.expires_in;

                                        /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                        AddOrUpdateAppSetting("PixVariables:token", token.access_token);
                                        AddOrUpdateAppSetting("PixVariables:inicio", _dtmInicioToken);*/
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);

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
                                                //Homologação
                                                HttpResponseMessage response = client.GetAsync("https://qrpix-h.bradesco.com.br/v1/spi/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString()).Result;

                                                //Produção
                                                //HttpResponseMessage response = client.GetAsync("https://qrpix.bradesco.com.br/v1/spi/cob/" + dtt_trans_pix.Rows[i]["str_txid"].ToString()).Result;

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
                                                                dtt_trans_grv = CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
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

                                                                stbTran = SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                transaction.Commit();
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                transaction.Rollback();
                                                                throw ex;
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

        public string ConsultaListaQRCODE()
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
            string str_data = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder str_json_tk = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            DataRow row = null;

            HttpResponseMessage respToken = new HttpResponseMessage();
            ConsListQRCODE_237 reg_ret = new ConsListQRCODE_237();

            using (SqlConnection conn = new SqlConnection(_strConnect))
            {
                conn.Open();
                try
                {
                    dtt_trans_pix = ConsultaTransacaoPIX(0, "", 0, StatusPIX.Ativa, 2, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);

                    if (dtt_trans_pix.Rows.Count > 0)
                    {
                        var cert = new X509Certificate2();
                        cert = SelecionaCertificado(dtt_trans_pix.Rows[0]["int_cnpj"].ToString(), "1234");

                        var handler = new HttpClientHandler();
                        handler.ClientCertificates.Add(cert);

                        using (var client = new HttpClient(handler))
                        {

                            //Busca autorização
                            str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_trans_pix.Rows[0]["str_senha_bco"].ToString();
                            encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                            encodedTxt = Convert.ToBase64String(encodedBytes);

                            str_json.Remove(0, str_json.Length);
                            client.DefaultRequestHeaders.Accept.Clear();
                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                            var dict = new Dictionary<string, string>();
                            //Brasil
                            dict.Add("grant_type", "client_credentials");
                            dict.Add("scope", "cob.read cob.write pix.read pix.write");
                            FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                            TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                            //token.expires_in = Convert.ToInt64(_config.GetSection("PixVariables")["expire"].ToString());
                            //token.expires_in = Convert.ToInt64(obj_config["PixVariables"]["expire"].ToString());

                            if (token.expires_in < Convert.ToInt64(tempo.TotalSeconds))
                            {
                                //Brasil
                                respToken = client.PostAsync("https://oauth.hm.bb.com.br/oauth/token", fencode).Result;
                                str_token = respToken.Content.ReadAsStringAsync().Result;
                            }
                            /*else
                            {
                                token = new Token
                                {
                                    //access_token = _config.GetSection("PixVariables")["token"].ToString(),
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
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                    _intExpire = token.expires_in;

                                    /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                    AddOrUpdateAppSetting("PixVariables:token", token.access_token);
                                    */
                                    AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                    AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);

                                    if (token.access_token.Length > 0)
                                    {

                                        for (int i = 0; i < dtt_trans_pix.Rows.Count; i++)
                                        {
                                            // Associar o token aos headers do objeto
                                            // do tipo HttpClient
                                            client.DefaultRequestHeaders.Accept.Clear();
                                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token.access_token);

                                            str_data = DateTimeOffset.Now.ToString("yyyy-MM-dd");
                                            str_json.Remove(0, str_json.Length);
                                            str_json.Append("?gw-dev-app-key=" + dtt_trans_pix.Rows[i]["str_chave_app"].ToString());
                                            str_json.Append("&inicio=" + str_data + "T00:00:00Z");
                                            str_json.Append("&fim=" + str_data + "T23:59:59Z");
                                            str_json.Append("&cpf");
                                            str_json.Append("&cnpj");
                                            str_json.Append("&paginaAtual=" + int_pag.ToString());
                                            str_json.Append("&itensPorPagina=100");

                                            //Envia om para o banco
                                            HttpResponseMessage response = client.GetAsync("https://api.hm.bb.com.br/pix/v1/pix/" + str_json.ToString()).Result;

                                            if (response.StatusCode == HttpStatusCode.OK)
                                            {
                                                str_ret = response.Content.ReadAsStringAsync().Result;
                                                if (str_ret.Length > 0)
                                                {
                                                    reg_ret = JsonConvert.DeserializeObject<ConsListQRCODE_237>(str_ret);

                                                    int_pag = 0;
                                                    int_pagAtual = 1;
                                                    int_pagTotal = reg_ret.parametros.paginacao.quantidadeDePaginas;

                                                    for (int_pag = 1; int_pag <= int_pagTotal; int_pag++)
                                                    {
                                                        if (int_pag > 1)
                                                        {
                                                            TimeSpan tempo1 = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                                            if (_intExpire < Convert.ToInt32(tempo1.TotalSeconds))
                                                            {
                                                                respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", new StringContent(str_json_tk.ToString(), Encoding.UTF8, "application/json")).Result;
                                                                str_token = respToken.Content.ReadAsStringAsync().Result;
                                                            }
                                                            if (str_token.Contains("acess_token"))
                                                            {
                                                                if (respToken.StatusCode == HttpStatusCode.OK)
                                                                {
                                                                    token = JsonConvert.DeserializeObject<Token>(str_token);
                                                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                                                    _intExpire = token.expires_in;

                                                                    if (token.access_token.Length > 0)
                                                                    {

                                                                        // Associar o token aos headers do objeto
                                                                        // do tipo HttpClient
                                                                        client.DefaultRequestHeaders.Accept.Clear();
                                                                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token.access_token);

                                                                        str_data = DateTimeOffset.Now.ToString("yyyy-MM-dd");
                                                                        str_json.Remove(0, str_json.Length);
                                                                        str_json.Append("?gw-dev-app-key=" + dtt_trans_pix.Rows[i]["str_chave_app"].ToString());
                                                                        str_json.Append("&inicio=" + str_data + "T00:00:00Z");
                                                                        str_json.Append("&fim=" + str_data + "T23:59:59Z");
                                                                        str_json.Append("&cpf");
                                                                        str_json.Append("&cnpj");
                                                                        str_json.Append("&paginaAtual=" + int_pag.ToString());
                                                                        str_json.Append("&itensPorPagina=100");

                                                                        //Envia om para o banco
                                                                        HttpResponseMessage response2 = client.GetAsync("https://api.hm.bb.com.br/pix/v1/pix/" + str_json.ToString()).Result;


                                                                        if (response2.StatusCode == HttpStatusCode.OK)
                                                                        {
                                                                            str_ret = response.Content.ReadAsStringAsync().Result;
                                                                            if (str_ret.Length > 0)
                                                                            {
                                                                                reg_ret = JsonConvert.DeserializeObject<ConsListQRCODE_237>(str_ret);
                                                                            }

                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }

                                                        dtt_trans_grv = CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn);

                                                        if (reg_ret.cobs.Count > 0)
                                                        {
                                                            for (int p = 0; p < reg_ret.cobs.Count; p++)
                                                            {
                                                                using (SqlTransaction transaction = conn.BeginTransaction())
                                                                {
                                                                    try
                                                                    {
                                                                        dtt_trans_grv.Rows.Clear();
                                                                        row = dtt_trans_grv.NewRow();
                                                                        row["id"] = -1;
                                                                        row["id_chavepix"] = null;
                                                                        row["str_txid"] = reg_ret.cobs[p].tx_id;
                                                                        row["int_expiracao"] = null;
                                                                        row["int_cpf_dev"] = reg_ret.cobs[p].devedor.cpf;
                                                                        row["int_cnpj_dev"] = reg_ret.cobs[p].devedor.cnpj;
                                                                        row["str_nome_dev"] = reg_ret.cobs[p].devedor.nome;
                                                                        row["dbl_valor_orig"] = reg_ret.cobs[p].valor.original;
                                                                        row["str_msg_devedor"] = reg_ret.cobs[p].solicitacaopagador;
                                                                        row["str_data_cria"] = null;
                                                                        row["int_revisao"] = null;
                                                                        row["str_location"] = reg_ret.cobs[p].location;
                                                                        row["int_cpf_pag"] = reg_ret.cobs[p].pix[0].pagador.cpf;
                                                                        row["int_cnpj_pag"] = reg_ret.cobs[p].pix[0].pagador.cnpj;
                                                                        row["str_nome_pag"] = reg_ret.cobs[p].pix[0].pagador.nome;
                                                                        row["str_msg_pagador"] = reg_ret.cobs[p].pix[0].infoPagador;
                                                                        row["str_id_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].Id;
                                                                        row["str_rtrid_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].rtrId;
                                                                        row["dbl_valor_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].valor;
                                                                        row["dtm_hora_sol_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].horario.solicitacao;
                                                                        row["dtm_hora_liq_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].horario.liquidacao;
                                                                        row["int_sit_devol"] = (reg_ret.cobs[p].pix[0].devolucoes[0].status == "EM_PROCESSAMENTO" ? 0 : (reg_ret.cobs[p].pix[0].devolucoes[0].status == "DEVOLVIDO" ? 1 : 2));
                                                                        row["str_idfim"] = reg_ret.cobs[p].pix[0].endToEndId;
                                                                        row["int_filial"] = null;
                                                                        row["int_tipoped"] = null;
                                                                        row["int_pedido"] = null;
                                                                        row["int_operador"] = null;
                                                                        row["int_caixa"] = null;
                                                                        row["str_emv"] = null;
                                                                        row["int_situacao"] = (reg_ret.cobs[p].status == "ATIVA" ? 1 : (reg_ret.cobs[p].status == "CONCLUIDA" ? 2 : (reg_ret.cobs[p].status == "REMOVIDA_PELO_USUARIO_RECEBEDOR" ? 3 : 4)));
                                                                        row["int_usu_lib"] = DBNull.Value;
                                                                        row["int_usu_dev"] = DBNull.Value;
                                                                        dtt_trans_grv.Rows.Add(row);

                                                                        stbTran = SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                        str_ret = ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);
                                                                    }
                                                                    catch (Exception ex)
                                                                    {
                                                                        transaction.Rollback();
                                                                        throw ex;
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
                    conn.Close();
                    throw ex;
                }
                conn.Close();
                return str_retorno;
            }
            
        }

        public string ConsultaListaQRCODEBradesco()
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
            DataRow row = null;

            HttpResponseMessage respToken = new HttpResponseMessage();
            ConsListQRCODE_237 reg_ret = new ConsListQRCODE_237();

            using (SqlConnection conn = new SqlConnection(_strConnect))
            {
                conn.Open();
                try
                {
                    dtt_trans_pix = ConsultaTransacaoPIX(0, "", 0, StatusPIX.Ativa, 2, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);

                    if (dtt_trans_pix.Rows.Count > 0)
                    {
                        var cert = new X509Certificate2();
                        //cert = SelecionaCertificado(dtt_trans_pix.Rows[0]["int_cnpj"].ToString(), "JjmlS2018");
                        cert = SelecionaCertificado("1438244000113", "JjmlS2018");
                        var handler = new HttpClientHandler();
                        handler.ClientCertificates.Add(cert);

                        if ((Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays < 30)
                        {
                            str_msg = "Certificado (" + cert.ToString() + ") será expirado em " + (Convert.ToDateTime(cert.GetExpirationDateString()) - DateTime.Now).TotalDays.ToString() + " dias.";
                            str_ret = EnviaEmailAviso(str_msg, "Aviso de expiração de certificado PIX", _configEmail);
                        }

                        using (var client = new HttpClient(handler))
                        {

                            //Busca autorização
                            str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + criptNtv.Descriptografar(dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                            encodedBytes = System.Text.Encoding.Unicode.GetBytes(str_credenciais);
                            encodedTxt = Convert.ToBase64String(encodedBytes);

                            str_json.Remove(0, str_json.Length);
                            client.DefaultRequestHeaders.Accept.Clear();
                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                            /*str_json.Append("{");
                            str_json.Append("\"grant_type\"" + ":" + "\"client_credentials\",");
                            str_json.Append("\"scope\":\"cob.write cob.read\" ");
                            str_json.Append("}");*/

                            var dict = new Dictionary<string, string>();
                            dict.Add("grant_type", "client_credentials");
                            //Bradesco
                            dict.Add("scope", "cob.read cob.write pix.read pix.write webhook.read webhook.write");
                            FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                            TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                            if (token.expires_in < Convert.ToInt64(tempo.TotalSeconds))
                            {
                                //Bradesco
                                respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", fencode).Result;
                                str_token = respToken.Content.ReadAsStringAsync().Result;
                            }
                            if (str_token.Contains("acess_token"))
                            {
                                if (respToken.StatusCode == HttpStatusCode.OK)
                                {
                                    token = JsonConvert.DeserializeObject<Token>(str_token);
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                    _intExpire = token.expires_in;
                                    if (token.access_token.Length > 0)
                                    {

                                        for (int i = 0; i < dtt_trans_pix.Rows.Count; i++)
                                        {
                                            // Associar o token aos headers do objeto
                                            // do tipo HttpClient
                                            client.DefaultRequestHeaders.Accept.Clear();
                                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token.access_token);

                                            str_json.Remove(0, str_json.Length);
                                            str_json.Append("{");
                                            str_json.Append("\"data_inicio_criacao\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["Dataini"].ToString() + "\",");
                                            str_json.Append("\"data_fim_criacao\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["Dataini"].ToString() + "\",");
                                            str_json.Append("\"cpf\"0\",");
                                            str_json.Append("\"cnpj\"0\",");
                                            str_json.Append("\"status\"" + ":" + "\"CONCLUIDA\",");
                                            str_json.Append("\"paginacao.paginaAtual\"" + ":" + "\"0\",");
                                            str_json.Append("\"paginacao.itensPorPagina\"" + ":" + "\"100\"");

                                            str_json.Append("}");

                                            //Envia om para o banco
                                            HttpResponseMessage response = client.GetAsync("https://qrpix-h.bradesco.com.br/v1/spi/cob/" + str_json.ToString()).Result;


                                            if (response.StatusCode == HttpStatusCode.OK)
                                            {
                                                str_ret = response.Content.ReadAsStringAsync().Result;
                                                if (str_ret.Length > 0)
                                                {
                                                    reg_ret = JsonConvert.DeserializeObject<ConsListQRCODE_237>(str_ret);

                                                    int_pag = 0;
                                                    int_pagAtual = 1;
                                                    int_pagTotal = reg_ret.parametros.paginacao.quantidadeDePaginas;

                                                    for (int_pag = 1; int_pag <= int_pagTotal; int_pag++)
                                                    {
                                                        if (int_pag > 1)
                                                        {
                                                            TimeSpan tempo1 = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                                            if (_intExpire < Convert.ToInt32(tempo1.TotalSeconds))
                                                            {
                                                                respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", new StringContent(str_json_tk.ToString(), Encoding.UTF8, "application/json")).Result;
                                                                str_token = respToken.Content.ReadAsStringAsync().Result;
                                                            }
                                                            if (str_token.Contains("acess_token"))
                                                            {
                                                                if (respToken.StatusCode == HttpStatusCode.OK)
                                                                {
                                                                    token = JsonConvert.DeserializeObject<Token>(str_token);
                                                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                                                    _intExpire = token.expires_in;

                                                                    if (token.access_token.Length > 0)
                                                                    {

                                                                        // Associar o token aos headers do objeto
                                                                        // do tipo HttpClient
                                                                        client.DefaultRequestHeaders.Accept.Clear();
                                                                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                                                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", token.access_token);

                                                                        str_json.Remove(0, str_json.Length);
                                                                        str_json.Append("{");
                                                                        str_json.Append("\"data_inicio_criacao\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["Dataini"].ToString() + "\",");
                                                                        str_json.Append("\"data_fim_criacao\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["Dataini"].ToString() + "\",");
                                                                        str_json.Append("\"cpf\"0\",");
                                                                        str_json.Append("\"cnpj\"0\",");
                                                                        str_json.Append("\"status\"" + ":" + "\"CONCLUIDA\",");
                                                                        str_json.Append("\"paginacao.paginaAtual\"" + ":" + "\"" + int_pag.ToString() + "\",");
                                                                        str_json.Append("\"paginacao.itensPorPagina\"" + ":" + "\"100\"");

                                                                        str_json.Append("}");

                                                                        //Envia om para o banco
                                                                        HttpResponseMessage response2 = client.GetAsync("https://qrpix-h.bradesco.com.br/v1/spi/cob/" + str_json.ToString()).Result;


                                                                        if (response2.StatusCode == HttpStatusCode.OK)
                                                                        {
                                                                            str_ret = response.Content.ReadAsStringAsync().Result;
                                                                            if (str_ret.Length > 0)
                                                                            {
                                                                                reg_ret = JsonConvert.DeserializeObject<ConsListQRCODE_237>(str_ret);
                                                                            }

                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }

                                                        dtt_trans_grv = CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn);

                                                        if (reg_ret.cobs.Count > 0)
                                                        {
                                                            for (int p = 0; p < reg_ret.cobs.Count; p++)
                                                            {
                                                                using (SqlTransaction transaction = conn.BeginTransaction())
                                                                {
                                                                    try
                                                                    {
                                                                        dtt_trans_grv.Rows.Clear();
                                                                        row = dtt_trans_grv.NewRow();
                                                                        row["id"] = -1;
                                                                        row["id_chavepix"] = null;
                                                                        row["str_txid"] = reg_ret.cobs[p].tx_id;
                                                                        row["int_expiracao"] = null;
                                                                        row["int_cpf_dev"] = reg_ret.cobs[p].devedor.cpf;
                                                                        row["int_cnpj_dev"] = reg_ret.cobs[p].devedor.cnpj;
                                                                        row["str_nome_dev"] = reg_ret.cobs[p].devedor.nome;
                                                                        row["dbl_valor_orig"] = reg_ret.cobs[p].valor.original;
                                                                        row["str_msg_devedor"] = reg_ret.cobs[p].solicitacaopagador;
                                                                        row["str_data_cria"] = null;
                                                                        row["int_revisao"] = null;
                                                                        row["str_location"] = reg_ret.cobs[p].location;
                                                                        row["int_cpf_pag"] = reg_ret.cobs[p].pix[0].pagador.cpf;
                                                                        row["int_cnpj_pag"] = reg_ret.cobs[p].pix[0].pagador.cnpj;
                                                                        row["str_nome_pag"] = reg_ret.cobs[p].pix[0].pagador.nome;
                                                                        row["str_msg_pagador"] = reg_ret.cobs[p].pix[0].infoPagador;
                                                                        row["str_id_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].Id;
                                                                        row["str_rtrid_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].rtrId;
                                                                        row["dbl_valor_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].valor;
                                                                        row["dtm_hora_sol_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].horario.solicitacao;
                                                                        row["dtm_hora_liq_devol"] = reg_ret.cobs[p].pix[0].devolucoes[0].horario.liquidacao;
                                                                        row["int_sit_devol"] = (reg_ret.cobs[p].pix[0].devolucoes[0].status == "EM_PROCESSAMENTO" ? 0 : (reg_ret.cobs[p].pix[0].devolucoes[0].status == "DEVOLVIDO" ? 1 : 2));
                                                                        row["str_idfim"] = reg_ret.cobs[p].pix[0].endToEndId;
                                                                        row["int_filial"] = null;
                                                                        row["int_tipoped"] = null;
                                                                        row["int_pedido"] = null;
                                                                        row["int_operador"] = null;
                                                                        row["int_caixa"] = null;
                                                                        row["str_emv"] = null;
                                                                        row["int_situacao"] = (reg_ret.cobs[p].status == "ATIVA" ? 1 : (reg_ret.cobs[p].status == "CONCLUIDA" ? 2 : (reg_ret.cobs[p].status == "REMOVIDA_PELO_USUARIO_RECEBEDOR" ? 3 : 4)));
                                                                        row["int_usu_lib"] = DBNull.Value;
                                                                        row["int_usu_dev"] = DBNull.Value;

                                                                        dtt_trans_grv.Rows.Add(row);

                                                                        stbTran = SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                        str_ret = ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);
                                                                    }
                                                                    catch (Exception ex)
                                                                    {
                                                                        transaction.Rollback();
                                                                        throw ex;
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
                    conn.Close();
                    throw ex;
                }
                conn.Close();
                return str_retorno;
            }

        }

        public string DevolucaoQRCODE(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";
            string str_id = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        dtt_trans_pix = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Devolver, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            //Produção
                            var cert = new X509Certificate2(".\\13850516000177.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                            //Local produção
                            //var cert = new X509Certificate2(@"C:\Jackson\Clientes\Guaibim\certificados\13850516000177.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                            //Local homologação
                            //var cert = new X509Certificate2(@"C:\Jackson\Clientes\Guaibim\certificados\13850516000924.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
                            //var cert = new X509Certificate2(@".\13850516000924.pfx", "1234", X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(cert);

                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_trans_pix.Rows[0]["str_senha_bco"].ToString();
                                encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                                encodedTxt = Convert.ToBase64String(encodedBytes);

                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                                var dict = new Dictionary<string, string>();
                                dict.Add("grant_type", "client_credentials");
                                //Bradesco
                                dict.Add("scope", "cob.read cob.write pix.read pix.write webhook.read webhook.write");
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                //_dtmInicioToken = GetAppSetting("PixVariables:inicio");
                                //_dtmInicioToken = obj_config["PixVariables"]["inicio"].ToString();

                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                if (token.expires_in < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    //Bradesco
                                    respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", fencode).Result; //Produção
                                    //respToken = client.PostAsync("https://qrpix.bradesco.com.br/auth/server/oauth/token", fencode).Result; //Homologação
                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                    _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                }
                                /*else
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

                                        /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                        AddOrUpdateAppSetting("PixVariables:token", token.access_token);
                                        AddOrUpdateAppSetting("PixVariables:inicio", _dtmInicioToken);*/
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:inicio", _dtmInicioToken);

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
                                                str_json.Append("\"e2eid\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "\",");
                                                str_json.Append("\"id\"" + ":" + "\"" + str_id + "\",");
                                                str_json.Append("\"valor\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["dbl_valor_devol"].ToString().Replace(",",".") + "\"");

                                                if (i + 1 == dtt_trans_pix.Rows.Count)
                                                {
                                                    str_json.Append("}");
                                                }
                                                else
                                                {
                                                    str_json.Append("},");
                                                }

                                                //Envia om para o banco
                                                //Homologação
                                                HttpResponseMessage response = client.PutAsync("https://qrpix-h.bradesco.com.br/v1/spi/pix/" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "/devolucao/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;

                                                //Produção
                                                //HttpResponseMessage response = client.PutAsync("https://qrpix.bradesco.com.br/v1/spi/pix/" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "/devolucao/" + dtt_trans_pix.Rows[i]["str_txid"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;


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
                                                                dtt_trans_grv = CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
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

                                                                stbTran = SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                transaction.Commit();
                                                            }
                                                            catch (Exception ex)
                                                            {
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

        public string ConsDevolucaoQRCODE(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();

            if (pedido.Filial > 0  && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        dtt_trans_pix = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Concluída, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            var cert = new X509Certificate2();
                            cert = SelecionaCertificado(dtt_trans_pix.Rows[0]["int_cnpj"].ToString(), "1234");
                            //cert = SelecionaCertificado("1438244000113", "JjmlS2018");
                            var handler = new HttpClientHandler();
                            handler.ClientCertificates.Add(cert);

                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_trans_pix.Rows[0]["str_senha_bco"].ToString();
                                encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                                encodedTxt = Convert.ToBase64String(encodedBytes);

                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                                var dict = new Dictionary<string, string>();
                                dict.Add("grant_type", "client_credentials");
                                //Bradesco
                                dict.Add("scope", "cob.read cob.write pix.read pix.write webhook.read webhook.write");
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                if (token.expires_in < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    //Bradesco
                                    respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", fencode).Result;
                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                }
                                /*else
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
                                        _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                        _intExpire = token.expires_in;

                                        /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                        AddOrUpdateAppSetting("PixVariables:token", token.access_token);
                                        */
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);

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
                                                str_json.Append("\"e2eid\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "\",");
                                                str_json.Append("\"id\"" + ":" + "\"" + dtt_trans_pix.Rows[i]["str_id_devol"].ToString() + "\"");

                                                if (i + 1 == dtt_trans_pix.Rows.Count)
                                                {
                                                    str_json.Append("}");
                                                }
                                                else
                                                {
                                                    str_json.Append("},");
                                                }

                                                //Envia om para o banco
                                                //GET https://qrpix-h.bradesco.com.br/v1/spi/pix/{e2eid}/devolução/{id}
                                                HttpResponseMessage response = client.GetAsync("https://qrpix-h.bradesco.com.br/v1/spi/pix/" + dtt_trans_pix.Rows[i]["str_idfim"].ToString() + "/devolucao/" + dtt_trans_pix.Rows[i]["str_txid"].ToString()).Result;


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
                                                                dtt_trans_grv = CriaDataTable("dbNatividade", "ntv_tbl_transacao_pix", "", conn, transaction);
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
                                                                row["str_data_cria"] = dtt_trans_pix.Rows[i]["str_data_cria"];
                                                                row["int_revisao"] = dtt_trans_pix.Rows[i]["int_revisao"];
                                                                row["str_location"] = dtt_trans_pix.Rows[i]["str_location"];
                                                                row["int_cpf_pag"] = dtt_trans_pix.Rows[i]["int_cpf_pag"];
                                                                row["int_cnpj_pag"] = dtt_trans_pix.Rows[i]["int_cnpj_pag"];
                                                                row["str_nome_pag"] = dtt_trans_pix.Rows[i]["str_nome_pag"];
                                                                row["str_msg_pagador"] = dtt_trans_pix.Rows[i]["str_msg_pagador"];
                                                                row["str_id_devol"] = dtt_trans_pix.Rows[i]["str_txid"].ToString() + "dev";
                                                                row["str_rtrid_devol"] = reg_ret.rtrid;
                                                                row["dbl_valor_devol"] = reg_ret.valor;
                                                                row["dtm_hora_sol_devol"] = reg_ret.horario.solicitacao;
                                                                row["dtm_hora_liq_devol"] = reg_ret.horario.liquidacao;
                                                                row["int_sit_devol"] = (reg_ret.status == "EM_PROCESSAMENTO" ? 0 : (reg_ret.status == "DEVOLVIDO" ? 1 : 2));
                                                                row["str_idfim"] = dtt_trans_pix.Rows[i]["str_idfim"];
                                                                row["int_filial"] = dtt_trans_pix.Rows[i]["int_filial"];
                                                                row["int_tipoped"] = dtt_trans_pix.Rows[i]["int_tipoped"];
                                                                row["int_pedido"] = dtt_trans_pix.Rows[i]["int_pedido"];
                                                                row["int_operador"] = dtt_trans_pix.Rows[i]["int_operador"];
                                                                row["int_caixa"] = dtt_trans_pix.Rows[i]["int_caixa"];
                                                                row["str_emv"] = dtt_trans_pix.Rows[i]["str_emv"];
                                                                row["int_situacao"] = dtt_trans_pix.Rows[i]["int_situacao"];
                                                                row["int_usu_lib"] = DBNull.Value;
                                                                row["int_usu_dev"] = DBNull.Value;

                                                                dtt_trans_grv.Rows.Add(row);

                                                                stbTran = SaveThroughXML(dtt_trans_grv, "ntv_tbl_transacao_pix");
                                                                str_ret = ManutencaoTabela("U", stbTran.ToString(), "tbl_transacao_pix", conn, transaction);

                                                                transaction.Commit();
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                transaction.Rollback();
                                                                throw ex;
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

        public void ReplicaPedido(int Filial, int Tipo, Int64 Pedido, SqlConnection conn, SqlTransaction tran)
        {

            SqlCommand command = new SqlCommand("ntv_p_replica_ped_distancia_pix", conn);
            command.Transaction = tran;
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@int_codfil", Filial));
            command.Parameters.Add(new SqlParameter("@int_tipoped", Tipo));
            command.Parameters.Add(new SqlParameter("@int_numpedven", Pedido));

            command.ExecuteNonQuery();
        }

        public void FaturaPedido(int Filial, int Tipo, Int64 Pedido, SqlConnection conn, SqlTransaction tran)
        {

            SqlCommand command = new SqlCommand("ntv_p_fatura_ped_distancia_pix", conn);
            command.Transaction = tran;
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@p_codfil", Filial));
            command.Parameters.Add(new SqlParameter("@p_tipoped", Tipo));
            command.Parameters.Add(new SqlParameter("@p_numpedven", Pedido));

            command.ExecuteNonQuery();
        }

        public DataTable ConsultaTransacaoPIX(int Filial, string Tipo, Int64 Pedido, StatusPIX Situacao, int Itens, DateTime DtIni, DateTime DtFim, int Caixa, SqlConnection conn, SqlTransaction tran)
        {

            SqlCommand command = new SqlCommand("ntv_p_sel_tbl_transacao_pix", conn);
            command.Transaction = tran;
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@id", -1));
            command.Parameters.Add(new SqlParameter("@id_empresa", -1));
            command.Parameters.Add(new SqlParameter("@cnpj", -1));
            command.Parameters.Add(new SqlParameter("@banco ", -1));
            command.Parameters.Add(new SqlParameter("@chave_pix", ""));
            command.Parameters.Add(new SqlParameter("@str_txid", ""));
            command.Parameters.Add(new SqlParameter("@int_codfil", Filial));
            command.Parameters.Add(new SqlParameter("@str_tipoped", Tipo));
            command.Parameters.Add(new SqlParameter("@int_pedido", Pedido));
            command.Parameters.Add(new SqlParameter("@int_operador", -1));
            command.Parameters.Add(new SqlParameter("@int_caixa", Caixa));
            command.Parameters.Add(new SqlParameter("@Itens", Itens));
            command.Parameters.Add(new SqlParameter("@situacao", Convert.ToInt16(Situacao)));
            command.Parameters.Add(new SqlParameter("@DtIni", DtIni));
            command.Parameters.Add(new SqlParameter("@DtFim", DtFim));

            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_retorno.Load(reader);
            }

            return dtt_retorno;
        }

        public DataTable ConsultaTransacaoPIX(Int64 id, string Tipo, int Itens, int Caixa, SqlConnection conn)
        {

            SqlCommand command = new SqlCommand("ntv_p_sel_tbl_transacao_pix", conn);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@id", id));
            command.Parameters.Add(new SqlParameter("@id_empresa", -1));
            command.Parameters.Add(new SqlParameter("@cnpj", -1));
            command.Parameters.Add(new SqlParameter("@banco ", -1));
            command.Parameters.Add(new SqlParameter("@chave_pix", ""));
            command.Parameters.Add(new SqlParameter("@str_txid", ""));
            command.Parameters.Add(new SqlParameter("@int_codfil", -1));
            command.Parameters.Add(new SqlParameter("@str_tipoped", Tipo));
            command.Parameters.Add(new SqlParameter("@int_pedido", -1));
            command.Parameters.Add(new SqlParameter("@int_operador", -1));
            command.Parameters.Add(new SqlParameter("@int_caixa", Caixa));
            command.Parameters.Add(new SqlParameter("@Itens", Itens));
            command.Parameters.Add(new SqlParameter("@situacao", -1));
            command.Parameters.Add(new SqlParameter("@DtIni", Convert.ToDateTime("2001-01-01")));
            command.Parameters.Add(new SqlParameter("@DtFim", Convert.ToDateTime("2001-01-01")));

            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_retorno.Load(reader);
            }

            return dtt_retorno;
        }

        public DataTable ConsultaTransacaoPIX(Int64 id, string Tipo, int Itens, int Caixa, SqlConnection conn, SqlTransaction tran)
        {

            SqlCommand command = new SqlCommand("ntv_p_sel_tbl_transacao_pix", conn);
            command.Transaction = tran;
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@id", id));
            command.Parameters.Add(new SqlParameter("@id_empresa", -1));
            command.Parameters.Add(new SqlParameter("@cnpj", -1));
            command.Parameters.Add(new SqlParameter("@banco ", -1));
            command.Parameters.Add(new SqlParameter("@chave_pix", ""));
            command.Parameters.Add(new SqlParameter("@str_txid", ""));
            command.Parameters.Add(new SqlParameter("@int_codfil", -1));
            command.Parameters.Add(new SqlParameter("@str_tipoped", Tipo));
            command.Parameters.Add(new SqlParameter("@int_pedido", -1));
            command.Parameters.Add(new SqlParameter("@int_operador", -1));
            command.Parameters.Add(new SqlParameter("@int_caixa", Caixa));
            command.Parameters.Add(new SqlParameter("@Itens", Itens));
            command.Parameters.Add(new SqlParameter("@situacao", -1));
            command.Parameters.Add(new SqlParameter("@DtIni", Convert.ToDateTime("2001-01-01")));
            command.Parameters.Add(new SqlParameter("@DtFim", Convert.ToDateTime("2001-01-01")));

            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_retorno.Load(reader);
            }

            return dtt_retorno;
        }

        public DataTable ConsultaTransacaoPIX(Int64 id, int Itens, int Caixa, SqlConnection conn)
        {

            SqlCommand command = new SqlCommand("ntv_p_sel_tbl_transacao_pix", conn);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@id", id));
            command.Parameters.Add(new SqlParameter("@id_empresa", -1));
            command.Parameters.Add(new SqlParameter("@cnpj", -1));
            command.Parameters.Add(new SqlParameter("@banco ", -1));
            command.Parameters.Add(new SqlParameter("@chave_pix", ""));
            command.Parameters.Add(new SqlParameter("@str_txid", ""));
            command.Parameters.Add(new SqlParameter("@int_codfil", -1));
            command.Parameters.Add(new SqlParameter("@str_tipoped", ""));
            command.Parameters.Add(new SqlParameter("@int_pedido", -1));
            command.Parameters.Add(new SqlParameter("@int_operador", -1));
            command.Parameters.Add(new SqlParameter("@int_caixa", Caixa));
            command.Parameters.Add(new SqlParameter("@Itens", Itens));
            command.Parameters.Add(new SqlParameter("@situacao", -1));
            command.Parameters.Add(new SqlParameter("@DtIni", Convert.ToDateTime("2001-01-01")));
            command.Parameters.Add(new SqlParameter("@DtFim", Convert.ToDateTime("2001-01-01")));

            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_retorno.Load(reader);
            }

            return dtt_retorno;
        }

        public DataTable ConsultaTransacaoPIX(Int64 id, int Itens, int Caixa, SqlConnection conn, SqlTransaction tran)
        {

            SqlCommand command = new SqlCommand("ntv_p_sel_tbl_transacao_pix", conn);
            command.Transaction = tran;
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@id", id));
            command.Parameters.Add(new SqlParameter("@id_empresa", -1));
            command.Parameters.Add(new SqlParameter("@cnpj", -1));
            command.Parameters.Add(new SqlParameter("@banco ", -1));
            command.Parameters.Add(new SqlParameter("@chave_pix", ""));
            command.Parameters.Add(new SqlParameter("@str_txid", ""));
            command.Parameters.Add(new SqlParameter("@int_codfil", -1));
            command.Parameters.Add(new SqlParameter("@str_tipoped", ""));
            command.Parameters.Add(new SqlParameter("@int_pedido", -1));
            command.Parameters.Add(new SqlParameter("@int_operador", -1));
            command.Parameters.Add(new SqlParameter("@int_caixa", Caixa));
            command.Parameters.Add(new SqlParameter("@Itens", Itens));
            command.Parameters.Add(new SqlParameter("@situacao", -1));
            command.Parameters.Add(new SqlParameter("@DtIni", Convert.ToDateTime("2001-01-01")));
            command.Parameters.Add(new SqlParameter("@DtFim", Convert.ToDateTime("2001-01-01")));

            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_retorno.Load(reader);
            }

            return dtt_retorno;
        }

        public DataTable ConsultaTransacaoPIX(int Filial, string Tipo, Int64 Pedido, StatusPIX Situacao, int Itens, DateTime DtIni, DateTime DtFim, int Caixa, SqlConnection conn)
        {

            SqlCommand command = new SqlCommand("ntv_p_sel_tbl_transacao_pix", conn);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@id", -1));            
            command.Parameters.Add(new SqlParameter("@id_empresa", -1));
            command.Parameters.Add(new SqlParameter("@cnpj", -1));
            command.Parameters.Add(new SqlParameter("@banco", -1));
            command.Parameters.Add(new SqlParameter("@chave_pix", ""));
            command.Parameters.Add(new SqlParameter("@str_txid", ""));
            command.Parameters.Add(new SqlParameter("@int_codfil", Filial));
            command.Parameters.Add(new SqlParameter("@str_tipoped", Tipo));
            command.Parameters.Add(new SqlParameter("@int_pedido", Pedido));
            command.Parameters.Add(new SqlParameter("@int_operador", -1));
            command.Parameters.Add(new SqlParameter("@int_caixa", Caixa));
            command.Parameters.Add(new SqlParameter("@Itens", Itens));
            command.Parameters.Add(new SqlParameter("@situacao", Convert.ToInt16(Situacao)));
            command.Parameters.Add(new SqlParameter("@DtIni", DtIni));
            command.Parameters.Add(new SqlParameter("@DtFim", DtFim));

            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_retorno.Load(reader);
            }

            return dtt_retorno;
        }

        public DataTable ConsultaChavePIX(Int64 id, Int64 id_empresa, Int64 cnpj, int Banco, string chave, int situacao, SqlConnection conn, SqlTransaction tran)
        {
            SqlCommand command = new SqlCommand("ntv_p_sel_tbl_emp_cc_chave_pix", conn);
            command.Transaction = tran;
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@id", id));
            command.Parameters.Add(new SqlParameter("@id_empresa", id_empresa));
            command.Parameters.Add(new SqlParameter("@cnpj", cnpj));
            command.Parameters.Add(new SqlParameter("@banco", Banco));
            command.Parameters.Add(new SqlParameter("@chave_pix", chave));
            command.Parameters.Add(new SqlParameter("@situacao", situacao));



            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_retorno.Load(reader);
            }

            return dtt_retorno;

        }
        public DataTable ConsultaUsuarios(string Login, SqlConnection conn)
        {
            SqlCommand command = new SqlCommand("ntv_p_sel_tbl_usuarios", conn);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@UserID", Login));
            command.Parameters.Add(new SqlParameter("@Senha", 1));


            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_retorno.Load(reader);
            }

            return dtt_retorno;

        }
        public X509Certificate2 SelecionaCertificado(string cnpj, string Senha)
        {
            X509Store stores = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            X509Certificate2Collection certificadosEncontrados = null;

            string str_retorno = "";
            try
            {
                // Abre o Store
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Abre o Store usuario : ");
                stores.Open(OpenFlags.ReadOnly);

                // Obtém a coleção dos certificados da Store
                X509Certificate2Collection certificados = stores.Certificates;
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Obtém a coleção dos certificados da Store ");
                //X509Certificate2Collection certificadosEncontrados = certificados.Find(X509FindType.FindBySerialNumber, "3FA452446149A5D1C7F3DD6235309B82",false);
                if (cnpj.Length > 0)
                {
                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Certificados encontrados " + certificados.Count);
                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Certificado dados " + certificados[0].FriendlyName + " - " + certificados[0].ToString());

                    certificadosEncontrados = certificados.Find(X509FindType.FindBySubjectName, cnpj, false);
                    _logger.LogInformation(DateTime.Now.ToString("G") + "-- Certificados encontrados por cnpj " + certificadosEncontrados.Count);
                    if (certificadosEncontrados.Count == 0)
                    {
                        certificadosEncontrados = certificados.Find(X509FindType.FindBySerialNumber, cnpj, false);
                        _logger.LogInformation(DateTime.Now.ToString("G") + "-- Selecionou certificado " + certificadosEncontrados.Count);
                    }
                }
                else
                {
                    str_retorno = "Não existem certificados instalados.";

                }

                if (certificadosEncontrados.Count > 0)
                {
                    return certificadosEncontrados[0];
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation(DateTime.Now.ToString("G") + "-- Erro Seleciona certificado : " + ex.Message.ToString());
                throw ex;
            }
        }

        public string GeraStringQRCODE(DataRow dtt_transacao)
        {
            string str_retorno = "";
            int int_tamanho = 0;
            int int_tamanho_tot = 0;
            StringBuilder str_qrcode = new StringBuilder();
            StringBuilder str_blc_26 = new StringBuilder();
            try
            {
                //Monta bloco do ID 26                
                int_tamanho = "br.gov.bcb.spi".Length;
                int_tamanho_tot += 2 + int_tamanho.ToString().Length + int_tamanho;
                str_blc_26.Append("00" + int_tamanho.ToString() + "br.gov.bcb.pix");

                /*int_tamanho = dtt_transacao["int_banco"].ToString().Length;
                str_blc_26.Append("21" + int_tamanho.ToString() + dtt_transacao["int_banco"].ToString());
                int_tamanho_tot += 2 + int_tamanho.ToString().Length + int_tamanho;

                int_tamanho = dtt_transacao["str_tipo"].ToString().Length;
                str_blc_26.Append("22" + int_tamanho.ToString() + dtt_transacao["str_tipo"].ToString());
                int_tamanho_tot += 2 + int_tamanho.ToString().Length + int_tamanho;

                int_tamanho = dtt_transacao["int_agencia"].ToString().Length;
                str_blc_26.Append("23" + int_tamanho.ToString() + dtt_transacao["int_agencia"].ToString());
                int_tamanho_tot += 2 + int_tamanho.ToString().Length + int_tamanho;

                int_tamanho = dtt_transacao["int_conta"].ToString().Length + dtt_transacao["str_digcta"].ToString().Length;
                str_blc_26.Append("24" + int_tamanho.ToString() + dtt_transacao["int_conta"].ToString() + dtt_transacao["str_digcta"].ToString());
                int_tamanho_tot += 2 + int_tamanho.ToString().Length + int_tamanho;*/

                int_tamanho = dtt_transacao["str_location"].ToString().Length;
                str_blc_26.Append("25" + int_tamanho.ToString() + dtt_transacao["str_location"].ToString());
                int_tamanho_tot += 2 + int_tamanho.ToString().Length + int_tamanho;

                str_qrcode.Append("000201");
                str_qrcode.Append("010212");
                str_qrcode.Append("26");  //Merchant Account information (Informações da conta do comerciante
                str_qrcode.Append(int_tamanho_tot.ToString());  //Tamanho total do dados da conta
                str_qrcode.Append(str_blc_26.ToString());  //Bloco 26
                str_qrcode.Append("52040000");  //Merchant category code (código de categoria do comerciante) não informado
                str_qrcode.Append("5303986"); //Transaction Currency (moeda de transação) 986 moeda "R$"

                int_tamanho = dtt_transacao["dbl_valor_orig"].ToString().Length;
                str_qrcode.Append("54" + (int_tamanho < 10 ? "0" + int_tamanho.ToString() : int_tamanho.ToString()) + dtt_transacao["dbl_valor_orig"].ToString().Replace(',','.')); //Transaction amount (valor da transação) 123.55

                str_qrcode.Append("5802BR"); //Country code (codigo do pais) "BR"

                int_tamanho = (dtt_transacao["str_empresa"].ToString().Length > 25 ? 25 : dtt_transacao["str_empresa"].ToString().Length);
                str_qrcode.Append("59" + (int_tamanho < 10 ? "0" + int_tamanho.ToString() : int_tamanho.ToString()) + (dtt_transacao["str_empresa"].ToString().Length > 25 ? dtt_transacao["str_empresa"].ToString().Substring(0,25) : dtt_transacao["str_empresa"].ToString())); //Merchant name (nome do comerciante)

                int_tamanho = (dtt_transacao["str_cidade"].ToString().Length > 15 ? 15 : dtt_transacao["str_cidade"].ToString().Length); 
                str_qrcode.Append("60" + (int_tamanho < 10 ? "0" + int_tamanho.ToString() : int_tamanho.ToString()) + (dtt_transacao["str_cidade"].ToString().Length > 15 ? dtt_transacao["str_cidade"].ToString().Substring(0,15) : dtt_transacao["str_cidade"].ToString())); //Merchant city (cidade do comerciante)

                //Monta bloco do ID 62 dados adicionais
                /*
                int_tamanho = dtt_transacao["str_txid"].ToString().Length;
                int_tamanho_tot = 2 + int_tamanho.ToString().Length + int_tamanho;
                str_blc_26.Remove(0, str_blc_26.Length);
                str_blc_26.Append("05" + int_tamanho.ToString() + dtt_transacao["str_txid"].ToString());
                */
                int_tamanho = "***".Length;
                int_tamanho_tot = 2 + 2 + int_tamanho;
                str_blc_26.Remove(0, str_blc_26.Length);
                str_blc_26.Append("05" + ("00" + int_tamanho.ToString()).Substring(("00" + int_tamanho.ToString()).Length - 2, 2) + "***");


                str_qrcode.Append("62");  //Additional data field (Campos de dados adcionais)
                str_qrcode.Append(("00" + int_tamanho_tot.ToString()).Substring(("00" + int_tamanho_tot.ToString()).Length - 2, 2));  //Tamanho total do dados adicionais
                str_qrcode.Append(str_blc_26.ToString());  //Bloco 62

                //Monta bloco do ID 80 Unreserved templates
                /*
                int_tamanho = "br.gov.bcb.spi".Length;
                int_tamanho_tot = 2 + int_tamanho.ToString().Length + int_tamanho;
                str_blc_26.Remove(0, str_blc_26.Length);
                str_blc_26.Append("00" + int_tamanho.ToString() + "br.gov.bcb.spi");

                str_qrcode.Append("80");  //Unreserved templates (modelos não reservados)
                str_qrcode.Append(int_tamanho_tot.ToString());  //Tamanho total do modelos não reservados
                str_qrcode.Append(str_blc_26.ToString());  //Bloco 62*/

                //CRC416
                str_qrcode.Append("6304");
                var str_qr = Encoding.UTF8.GetBytes(str_qrcode.ToString());
                var str_crc16 =  NullFX.CRC.Crc16.ComputeChecksum(NullFX.CRC.Crc16Algorithm.CcittInitialValue0xFFFF, str_qr);
                int int_crc16 = str_crc16;
                str_qrcode.Append( ("0000" + int_crc16.ToString("X")).Substring(("0000" + int_crc16.ToString("X")).Length -4,4)); //Merchant name (nome do comerciante)
                str_retorno = str_qrcode.ToString();

                return str_retorno;
            }
            catch(Exception ex)
            {
                throw ex;
            }
        }
        public string GeraImagemQRCODE(string str_qrcode)
        {
            string str_retorno = "";
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    QRCodeGenerator qrcodegen = new QRCodeGenerator();
                    QRCodeData qrcodedata = qrcodegen.CreateQrCode(str_qrcode, QRCodeGenerator.ECCLevel.L);
                    QRCode qrcode = new QRCode(qrcodedata);
                    using (Bitmap bitmap = qrcode.GetGraphic(10))
                    {
                        bitmap.Save(ms, ImageFormat.Jpeg);
                        str_retorno = "data:image/jpeg;base64," + Convert.ToBase64String(ms.ToArray());
                    }

                }

                return str_retorno;
            }
            catch (Exception ex)
            {
                throw ex;
            }
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

        public string ManutencaoTabela(string Operacao, string Dados, string Tabela, SqlConnection conn, SqlTransaction tran)
        {

            try
            {
                SqlCommand command = conn.CreateCommand();
                SqlParameter str_retorno = new SqlParameter();
                str_retorno.Direction = ParameterDirection.Output;
                str_retorno.Size = 1000;
                str_retorno.ParameterName = "@Novos_id";
                str_retorno.SqlDbType = SqlDbType.VarChar;

                command.Transaction = tran;
                command.CommandText = "ntv_p_man_" + Tabela;
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@Tp_processo", Operacao));
                command.Parameters.Add(new SqlParameter("@Dados", Dados));
                command.Parameters.Add(str_retorno);

                command.ExecuteNonQuery();

                return str_retorno.Value.ToString();
            }
            catch (Exception ex)
            {
                throw ex;                
            }
        }
        public DataTable CriaDataTable(string Banco, string Tabela, string Campos, SqlConnection conn)
        {
            SqlCommand command = new SqlCommand("ntv_p_sel_estrutura_tabela", conn);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@Banco", Banco));
            command.Parameters.Add(new SqlParameter("@Tabela", Tabela));

            DataTable dtt_result = new DataTable();
            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_result.Load(reader);
            }

            if (dtt_result.Rows.Count > 0)
            {
                bool flag = false;

                for (int i = 0; i < dtt_result.Rows.Count; i++)
                {
                    string str;
                    if (Campos.Length > 0)
                    {
                        if (Campos.ToUpper().IndexOf(dtt_result.Rows[i]["coluna"].ToString().ToUpper()) > -1)
                        {
                            flag = true;
                        }
                        else
                        {
                            flag = false;
                        }
                    }
                    else
                    {
                        flag = true;
                    }
                    if (flag && ((str = dtt_result.Rows[i]["tipo"].ToString()) != null))
                    {
                        if ((!(str == "char") && !(str == "varchar")) && !(str == "datetime") && !(str == "time") && !(str == "nvarchar"))
                        {
                            if ((str == "numeric") || (str == "money") || (str == "decimal"))
                            {
                                goto Label_0182;
                            }
                            if (str == "bigint")
                            {
                                goto Label_02C7;
                            }
                            if (str == "int")
                            {
                                goto Label_02FB;
                            }
                        }
                        else
                        {
                            dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(string));
                        }
                    }
                    continue;
                Label_0182:
                    if (Convert.ToInt16(dtt_result.Rows[i]["decimal"].ToString()) > 0)
                    {
                        dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(double));
                    }
                    else if (Convert.ToInt16(dtt_result.Rows[i]["precisao"].ToString()) < 4)
                    {
                        dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int16));
                    }
                    else if (Convert.ToInt16(dtt_result.Rows[i]["precisao"].ToString()) < 6)
                    {
                        dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int32));
                    }
                    else
                    {
                        dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int64));
                    }
                    continue;
                Label_02C7:
                    dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int64));
                    continue;
                Label_02FB:
                    dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int32));
                }
                return dtt_retorno;
            }
            return dtt_retorno;

        }

        public DataTable CriaDataTable(string Banco, string Tabela, string Campos, SqlConnection conn, SqlTransaction tran)
        {
            SqlCommand command = new SqlCommand("ntv_p_sel_estrutura_tabela", conn);
            command.Transaction = tran;
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@Banco", Banco));
            command.Parameters.Add(new SqlParameter("@Tabela", Tabela));

            DataTable dtt_result = new DataTable();
            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_result.Load(reader);
            }

            if (dtt_result.Rows.Count > 0)
            {
                bool flag = false;

                for (int i = 0; i < dtt_result.Rows.Count; i++)
                {
                    string str;
                    if (Campos.Length > 0)
                    {
                        if (Campos.ToUpper().IndexOf(dtt_result.Rows[i]["coluna"].ToString().ToUpper()) > -1)
                        {
                            flag = true;
                        }
                        else
                        {
                            flag = false;
                        }
                    }
                    else
                    {
                        flag = true;
                    }
                    if (flag && ((str = dtt_result.Rows[i]["tipo"].ToString()) != null))
                    {
                        if ((!(str == "char") && !(str == "varchar")) && !(str == "datetime") && !(str == "time") && !(str == "nvarchar"))
                        {
                            if ((str == "numeric") || (str == "money") || (str == "decimal"))
                            {
                                goto Label_0182;
                            }
                            if (str == "bigint")
                            {
                                goto Label_02C7;
                            }
                            if (str == "int")
                            {
                                goto Label_02FB;
                            }
                        }
                        else
                        {
                            dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(string));
                        }
                    }
                    continue;
                Label_0182:
                    if (Convert.ToInt16(dtt_result.Rows[i]["decimal"].ToString()) > 0)
                    {
                        dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(double));
                    }
                    else if (Convert.ToInt16(dtt_result.Rows[i]["precisao"].ToString()) < 4)
                    {
                        dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int16));
                    }
                    else if (Convert.ToInt16(dtt_result.Rows[i]["precisao"].ToString()) < 6)
                    {
                        dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int32));
                    }
                    else
                    {
                        dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int64));
                    }
                    continue;
                Label_02C7:
                    dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int64));
                    continue;
                Label_02FB:
                    dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int32));
                }
                return dtt_retorno;
            }
            return dtt_retorno;

        }

        public string GeraQrcodePedido(Pedido pedido)
        {
            string str_retorno = "ok";
            string str_ret = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_itens = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();

            if (pedido.Filial > 0 && pedido.numpedido > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        dtt_trans_pix = ConsultaTransacaoPIX(pedido.Filial, pedido.Tipo.ToString(), pedido.numpedido, StatusPIX.Ativa, 0, Convert.ToDateTime("2001-01-01"), Convert.ToDateTime("2001-01-01"), -2, conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            str_ret = GeraStringQRCODE(dtt_trans_pix.Rows[0]);
                            str_ret = GeraImagemQRCODE(str_ret);
                            str_retorno = str_ret;
                        }
                        else
                        {
                            str_retorno = "Não existe pedido para geração de QR CODE.";
                        }

                    }
                    catch (Exception ex)
                    {
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

        public string GeraQrcodePIX(Int64 id)
        {
            string str_retorno = "ok";
            string str_ret = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_itens = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();

            if (id > 0)
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();
                    try
                    {
                        dtt_trans_pix = ConsultaTransacaoPIX(id, 0, -2, conn);

                        if (dtt_trans_pix.Rows.Count > 0)
                        {
                            str_ret = GeraStringQRCODE(dtt_trans_pix.Rows[0]);
                            str_ret = GeraImagemQRCODE(str_ret);
                            str_retorno = str_ret;
                        }
                        else
                        {
                            str_retorno = "Não existe PIX para geração de QR CODE.";
                        }

                    }
                    catch (Exception ex)
                    {
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

        public string PutWebHook()
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";            

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_chave = new DataTable();
            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_itens = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();

            using (SqlConnection conn = new SqlConnection(_strConnect))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        dtt_chave = ConsultaChavePIX(1, 0, 0, 0, "", 0, conn, transaction);

                        if (dtt_chave.Rows.Count > 0)
                        {
                            var cert = new X509Certificate2();
                            cert = SelecionaCertificado(dtt_chave.Rows[0]["int_cnpj"].ToString(), "1234");
                            //cert = SelecionaCertificado("1438244000113", "JjmlS2018");

                            var handler = new HttpClientHandler();
                            //Bradesco
                            handler.ClientCertificates.Add(cert);

                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                //str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + criptNtv.Descriptografar(dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                str_credenciais = dtt_chave.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_chave.Rows[0]["str_senha_bco"].ToString();
                                encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                                encodedTxt = Convert.ToBase64String(encodedBytes);

                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                                var dict = new Dictionary<string, string>();
                                dict.Add("grant_type", "client_credentials");
                                //Bradesco
                                dict.Add("scope", "webhook.write");
                                //Brasil
                                //dict.Add("scope", "pix.read pix.write cob.read cob.write");
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                if (token.expires_in < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    //Bradesco
                                    respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", fencode).Result;
                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                }
                                /*else
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
                                        _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                        _intExpire = token.expires_in;

                                        /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                        AddOrUpdateAppSetting("PixVariables:token", token.access_token);
                                        */
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);

                                        if (token.access_token.Length > 0)
                                        {


                                            // Associar o token aos headers do objeto
                                            // do tipo HttpClient
                                            client.DefaultRequestHeaders.Accept.Clear();
                                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                            str_json.Remove(0, str_json.Length);
                                            str_json.Append("{");
                                            //str_json.Append("\"webhookUrl\"" + ":" + "\"" + _webhookUrl + "\"");
                                            str_json.Append("\"webhookUrl\"" + ":" + "\"" + _webhookUrlHomologa + "\"");
                                            str_json.Append("}");
                                            
                                            HttpResponseMessage response = client.PutAsync("https://qrpix-h.bradesco.com.br/v1/spi/webhook/" + "14a3a60b-d431-4150-a1fc-41c5ffa57a31", new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;
                                            //HttpResponseMessage response = client.PutAsync("https://qrpix-h.bradesco.com.br/v1/spi/webhook/" + dtt_chave.Rows[0]["str_chavepix"].ToString(), new StringContent(str_json.ToString(), Encoding.UTF8, "application/json")).Result;

                                            if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK)
                                            {
                                                str_ret = response.Content.ReadAsStringAsync().Result;
                                                str_retorno = str_ret;
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
                                else
                                {
                                    str_ret = "Problemas na geração do Token.";
                                }

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
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        conn.Close();
                        throw ex;
                    }                    
                }
                conn.Close();
            }

            return str_retorno;
        }

        public string GetWebHook()
        {
            string str_retorno = "ok";
            string str_ret = "";
            string str_credenciais = "";
            byte[] encodedBytes = null;
            string encodedTxt = "";
            string str_token = "";

            StringBuilder str_json = new StringBuilder();
            StringBuilder stbTran = new StringBuilder();

            DataTable dtt_chave = new DataTable();
            DataTable dtt_trans_pix = new DataTable();
            DataTable dtt_trans_itens = new DataTable();
            DataTable dtt_trans_grv = new DataTable();
            HttpResponseMessage respToken = new HttpResponseMessage();

            using (SqlConnection conn = new SqlConnection(_strConnect))
            {
                conn.Open();
                using (SqlTransaction transaction = conn.BeginTransaction())
                {
                    try
                    {
                        dtt_chave = ConsultaChavePIX(1, 0, 0, 0, "", 0, conn, transaction);

                        if (dtt_chave.Rows.Count > 0)
                        {
                            var cert = new X509Certificate2();
                            cert = SelecionaCertificado(dtt_chave.Rows[0]["int_cnpj"].ToString(), "1234");
                            //cert = SelecionaCertificado("1438244000113", "JjmlS2018");

                            var handler = new HttpClientHandler();
                            //Bradesco
                            handler.ClientCertificates.Add(cert);

                            using (var client = new HttpClient(handler))
                            {

                                //Busca autorização
                                //str_credenciais = dtt_trans_pix.Rows[0]["str_usuario_bco"].ToString() + ":" + criptNtv.Descriptografar(dtt_trans_pix.Rows[0]["str_senha_bco"].ToString());
                                str_credenciais = dtt_chave.Rows[0]["str_usuario_bco"].ToString() + ":" + dtt_chave.Rows[0]["str_senha_bco"].ToString();
                                encodedBytes = System.Text.Encoding.ASCII.GetBytes(str_credenciais);
                                encodedTxt = Convert.ToBase64String(encodedBytes);

                                str_json.Remove(0, str_json.Length);
                                client.DefaultRequestHeaders.Accept.Clear();
                                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
                                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedTxt);

                                var dict = new Dictionary<string, string>();
                                dict.Add("grant_type", "client_credentials");
                                //Bradesco
                                dict.Add("scope", "webhook.read");
                                //Brasil
                                //dict.Add("scope", "pix.read pix.write cob.read cob.write");
                                FormUrlEncodedContent fencode = new FormUrlEncodedContent(dict);

                                TimeSpan tempo = DateTime.Now.Subtract(Convert.ToDateTime(_dtmInicioToken));
                                if (token.expires_in < Convert.ToInt64(tempo.TotalSeconds))
                                {
                                    //Bradesco
                                    respToken = client.PostAsync("https://qrpix-h.bradesco.com.br/auth/server/oauth/token", fencode).Result;
                                    //Brasil
                                    //respToken = client.PostAsync("https://oauth.hm.bb.com.br/oauth/token", fencode).Result;
                                    str_token = respToken.Content.ReadAsStringAsync().Result;
                                }
                                /*else
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
                                        _dtmInicioToken = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                        _intExpire = token.expires_in;

                                        /*AddOrUpdateAppSetting("PixVariables:expire", _intExpire);
                                        AddOrUpdateAppSetting("PixVariables:token", token.access_token);
                                        */
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:expire", _intExpire);
                                        AddOrUpdateJSON(".\\ConfigPIX.json", "PixVariables:token", token.access_token);

                                        if (token.access_token.Length > 0)
                                        {


                                            // Associar o token aos headers do objeto
                                            // do tipo HttpClient
                                            client.DefaultRequestHeaders.Accept.Clear();
                                            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                                            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.access_token);

                                            str_json.Remove(0, str_json.Length);
                                            str_json.Append("{");
                                            str_json.Append("\"webhookUrl\"" + ":" + "\"" + _webhookUrl + "\"");
                                            str_json.Append("}");

                                            //HttpResponseMessage response = client.GetAsync("https://qrpix-h.bradesco.com.br/v1/spi/webhook/" + dtt_chave.Rows[0]["str_chavepix"].ToString()).Result;
                                            HttpResponseMessage response = client.GetAsync("https://qrpix-h.bradesco.com.br/v1/spi/webhook/" + "14a3a60b-d431-4150-a1fc-41c5ffa57a31").Result;                                            

                                            if (response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.OK)
                                            {
                                                str_ret = response.Content.ReadAsStringAsync().Result;
                                                str_retorno = str_ret;
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
                                else
                                {
                                    str_ret = "Problemas na geração do Token.";
                                }

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
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        conn.Close();
                        throw ex;
                    }
                }
                conn.Close();
            }

            return str_retorno;
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

        private static void AddOrUpdateAppSetting<T>(string key, T value)
        {
            try
            {

                var filePath = Path.Combine(AppContext.BaseDirectory, "appSettings.json");
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
        private string GetAppSetting(string key)
        {
            try
            {

                var filePath = Path.Combine(AppContext.BaseDirectory, "appSettings.json");
                string json = File.ReadAllText(filePath);
                dynamic jsonObj = Newtonsoft.Json.JsonConvert.DeserializeObject(json);

                var sectionPath = key.Split(":")[0];
                if (!string.IsNullOrEmpty(sectionPath))
                {
                    var keyPath = key.Split(":")[1];
                    return jsonObj[sectionPath][keyPath];
                }
                else
                {
                    return ""; // if no sectionpath just set the value
                }                                
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        static bool VerificarCertificado(HttpRequestMessage sender, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            try
            {
                // Possibly required for iOS? :
                //if (chain.ChainElements.Count == 0) chain.Build(certificate);
                // https://forums.xamarin.com/discussion/180066/httpclienthandler-servercertificatecustomvalidationcallback-receives-empty-certchain
                // ^ Sorry that thread is such a mess!  But please check it.

                // Without having your PEM I am not sure if this approach to loading the cert works, but there are other ways.  From the doc:
                // "This constructor creates a new X509Certificate2 object using a certificate file name. It supports binary (DER) encoding or Base64 encoding."
                X509Certificate2 ca = new X509Certificate2("mypem.pem");

                X509Chain chain2 = new X509Chain();
                chain2.ChainPolicy.ExtraStore.Add(ca);

                // "tell the X509Chain class that I do trust this root certs and it should check just the certs in the chain and nothing else"
                chain2.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

                // This setup does not have revocation information
                chain2.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                // Build the chain and verify
                var isValid = chain2.Build(certificate);
                var chainRoot = chain2.ChainElements[chain2.ChainElements.Count - 1].Certificate;
                isValid = isValid && chainRoot.RawData.SequenceEqual(ca.RawData);

                return isValid;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return false;
        }

        public string EnviaEmailAviso(String str_mensagem, string objetivo, ConfigEmail config)
        {
            bool bol_ret = false;

            string str_corpo = "";
            //string[] ArrMail = emailBoleto.strEmail.Split(';');
            MailAddress remetente = null;
            MailAddress destinatario = null;
            MailAddress CC = null;

            try
            {
                remetente = new MailAddress(config.usuario, "Avisos");

                destinatario = new MailAddress(config.Email, "Avisos");

                MailMessage email = new MailMessage(remetente, destinatario);
                //Acrescentas os copiados
                if (config.EmailCC.Length > 0)
                {
                    foreach (string strCC in config.EmailCC.Split(';'))
                    {
                        if (strCC != "")
                        {
                            CC = new MailAddress(strCC.Trim());
                            email.CC.Add(CC);
                        }
                    }
                }

                SmtpClient c_email = new SmtpClient(config.servidorSMTP, config.porta);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls;
                NetworkCredential credencial = null;

                credencial = new NetworkCredential(config.usuario, config.senha);


                c_email.Credentials = credencial;
                email.Subject = objetivo;// "Aviso de expiração de Certificado";
                str_corpo = str_mensagem;
                email.IsBodyHtml = true;
                email.Body = str_corpo;

                c_email.Send(email);

                email.Dispose();
                return "ok";

            }
            catch (Exception ex)
            {
                //throw ex;
                //return "[" + str_mensagem + "]" + ex.Message.ToString().Replace("'", "").Replace("\n", "").Replace("\r", "");
                return ex.Message.ToString().Replace("'", "").Replace("\n", "").Replace("\r", "");
            }

        }

    }
}
/*

chave_pix               String                  obrigatório Chave Pix do recebedor cadastrada no sistema DICT – BACEN       O campo chave, obrigatório, determina a chave Pix registrada no DICT que será 
                                                                            utilizada para a cobrança. Essa chave será lida pelo aplicativo do PSP do pagador 
                                                                            para consulta ao DICT, que retornará informação que identificará o recebedor da 
                                                                            cobrança. Limitada 77 caracteres
tx_id                   String [AZ0-9-]{1,35}   obrigatória ID de identificação do QR entre instituições e cliente emissor. O campo txid, obrigatório, determina o identificador da transação. O objetivo desse 
                                                                            campo é ser um elemento que possibilite ao Bradesco apresentar ao usuário recebedor a 
                                                                            funcionalidade de conciliação de pagamentos.
                                                                            Na pacs.008, é referenciado como TransactionIdentification <txId> ou 
                                                                            idConciliacaoRecebedor. O preenchimento do campo txid é limitado a 35 caracteres na 
                                                                            pacs.008.
                                                                            Em termos de fluxo de funcionamento, o txid é lido pelo aplicativo do PSP do pagador 
                                                                            e, depois de confirmado o pagamento, é enviado para o SPI via pacs.008. Uma pacs.008 
                                                                            também é enviada ao Bradesco, contendo, além de todas as informações usuais do 
                                                                            pagamento, o txid. Ao perceber um recebimento dotado de txid, o Bradesco está apto a 
                                                                            se comunicar com o usuário recebedor, informando que um pagamento específico foi 
                                                                            liquidado.
                                                                            O txid é criado exclusivamente pelo usuário recebedor e está sob sua responsabilidade. 
                                                                            O txid, no contexto de representação de uma cobrança, é único por CPF/CNPJ do usuário 
                                                                            recebedor. Cabe ao Bradesco validar essa regra na API PIX e rejeitar a solicitação 
                                                                            caso não esteja de acordo com a regra. 
calendario              Objeto                  opcional    -                                                               Os campos aninhados sob o identificador calendário organizam informações a respeito 
                                                                            de controle de tempo da cobrança.
calendario.expiracao    Integer <int32>         opcional    Tempo de expiração de um QR Code informado em segundos.         Tempo de vida da cobrança, especificado em segundos a partir da data de 
                                                                            criação (calendario.criacao).Obrigatoriamente informado em segundos da data 
                                                                            de criação e permite que o pagamento seja realizado até a data de expiração 
                                                                            informada.Quando não informado default será 86400 segundos (24 horas). 
devedor                 Objeto                  opcional                                                                    Os campos aninhados sob o objeto devedor são opcionais e identificam o 
                                                                            devedor, ou seja, a pessoa ou a instituição a quem a cobrança está 
                                                                            endereçada. Não identifica, necessariamente, quem irá efetivamente realizar o 
                                                                            pagamento. Um CPF pode ser o devedor de uma cobrança, mas pode acontecer de 
                                                                            outro CPF realizar, efetivamente, o pagamento do documento. Não é permitido 
                                                                            que o campo pagador.cpf e campo pagador.cnpj estejam preenchidos ao mesmo 
                                                                            tempo. Se o campo pagador.cnpj está preenchido, então o campo pagador.cpf não 
                                                                            pode estar preenchido, e vice-versa. Se o campo pagador.nome está preenchido, 
                                                                            então deve existir ou um pagador.cpf ou um campo pagador.cnpj preenchido.
devedor.cpf             String /^\d{11}$/       opcional    Número do Documento Cadastro de Pessoa Física do pagador        Apenas CPF. (não enviar o devedor.cnpj). 
devedor.cnpj            String /^\d{14}$/       opcional    Número do Cadastro Nacional da Pessoa Jurídica do pagador       Apenas CNPJ (não enviar o devedor.cpf).
devedor.nome            String                  opcional    Nome do pagador do QR Code.                                     Se o campo nome estiver preenchido, obrigatoriamente deve ser preenchido 
                                                                            campo de CPF ou CNPJ. 
valor                   Objeto                  obrigatório -                                                               Campos de valor obedecem ao format do ID 54 da especificação EMV/BR Code para 
                                                                            QR Codes.
valor.original          String \d{1,10}\.\d{2}  obrigatório Valor nominal do QR Code.                                       O separador decimal é o caractere ponto.Não é aplicável utilizar separador de 
                                                                            milhar. Exemplos: “0.00”, “1.00”, “123.99”, “123456789.23”
                                                                            O valor deve ser maior que zero.Flag “permite alteração de valor” será igual 
                                                                            a não. Dessa forma, não será permitida edição do valor pelo devedor. 
solicitacaopagador      String                  opcional    Mensagem de solicitação do devedor ao emissor.                  O campo solicitacaopagador, opcional, determina um texto a ser 
                                                                            apresentado ao pagador para que ele possa digitar uma informação correlata, 
                                                                            em formato livre, a ser enviada ao recebedor. 
                                                                            Esse texto será preenchido, na pacs.008, pelo PSP do pagador, no campo 
                                                                            RemittanceInformation . O tamanho do campo na pacs.008 está limitado a 140 
                                                                            caracteres.
info_adicionais         Objeto                  opcional    -                                                               Trata-se de um array.Cada respectiva informação adicional contida na lista 
                                                                            (nome e valor) deve ser apresentada ao pagador. 
info_adicionais.nome    String                  opcional    Nome da chave da informação                                     Se for enviado, é necessário o envio do campo info_adicionais.valor. 
                                                                            (Tamanho máximo= 50).
info_adicionais.valor   String                  opcional    Valor da informação                                             
*/
