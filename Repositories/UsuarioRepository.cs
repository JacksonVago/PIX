using Microsoft.Extensions.Configuration;
using PIX.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PIX.Repositories
{
    
    public class UsuarioRepository
    {
        private readonly string _strConnect;

        public UsuarioRepository(IConfiguration config)
        {
            _strConnect = config.GetConnectionString("DeafultConnectionStrings") + "@DTILGCF06FW";
        }

        public async Task<Usuario> ValidaUsuario(string usuario, string senha)
        {
            Usuario user_ret = new Usuario();
            DataTable dtt_usuario = new DataTable();
            DataTable dtt_permissao = new DataTable();

            try
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();

                    using (SqlTransaction tran = conn.BeginTransaction())
                    {
                        try
                        {
                            dtt_usuario = ConsultaUsuarios(usuario, conn, tran);
                            if (dtt_usuario.Rows.Count > 0)
                            {
                                if (senha.ToUpper() == Descriptografar(dtt_usuario.Rows[0]["str_senha"].ToString()).ToUpper())
                                {

                                    /*Carrega permissão do usuário*/
                                    dtt_permissao = ConsultaUsuarioPermissao("APIPIX", 1, Convert.ToInt64(dtt_usuario.Rows[0]["id"]), conn, tran);

                                    if (dtt_permissao.Rows.Count > 0)
                                    {
                                        user_ret.id = Convert.ToInt64(dtt_usuario.Rows[0]["id"]);
                                        user_ret.username = dtt_usuario.Rows[0]["str_usuario"].ToString();
                                        user_ret.password = dtt_usuario.Rows[0]["str_senha"].ToString();
                                        user_ret.validade = DateTime.Now.AddMinutes(40);
                                    }
                                    else
                                    {
                                        user_ret.id = 0;
                                        user_ret.username = "Usuário sem permissão de acesso";
                                        user_ret.password = "";
                                        user_ret.validade = DateTime.Now.AddMinutes(30);

                                    }
                                }
                                else
                                {
                                    user_ret.id = 0;
                                    user_ret.username = "Senha inválida";
                                    user_ret.password = "";
                                    user_ret.validade = DateTime.Now.AddMinutes(30);
                                }
                            }
                            tran.Commit();
                        }
                        catch (Exception ex)
                        {
                            tran.Rollback();
                            conn.Close();
                            throw ex;

                        }
                    }
                    conn.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return user_ret;
        }
        public async Task<bool> GravarAcesso(Usuario usuario, string Token, string ip, string metodo, string parametros)
        {
            DataTable dtt_usuario_grv = new DataTable();
            DataRow row_usuario = null;
            StringBuilder stb_usuario = new StringBuilder();

            bool bol_ret = false;

            try
            {
                using (SqlConnection conn = new SqlConnection(_strConnect))
                {
                    conn.Open();

                    using (SqlTransaction tran = conn.BeginTransaction())
                    {
                        try
                        {
                            dtt_usuario_grv = ConsultaAcesso(-1, conn, tran);
                            row_usuario = dtt_usuario_grv.NewRow();

                            row_usuario["id"] = 0;
                            row_usuario["id_usuario"] = usuario.id;
                            row_usuario["dtm_acesso"] = DateTime.Now;
                            row_usuario["dtm_validade"] = usuario.validade;
                            row_usuario["str_token"] = Token;
                            row_usuario["str_ip"] = ip;
                            row_usuario["str_metodo"] = metodo;
                            row_usuario["str_parametros"] = parametros;
                            row_usuario["int_nivel"] = 0;

                            dtt_usuario_grv.Rows.Add(row_usuario);
                            stb_usuario = SaveThroughXML(dtt_usuario_grv, "ntv_tbl_token_acesso");
                            bol_ret = InsAcesso(stb_usuario.ToString(), conn, tran);

                            tran.Commit();
                        }
                        catch (Exception ex)
                        {
                            tran.Rollback();
                            conn.Close();
                            throw ex;

                        }
                    }
                    conn.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return bol_ret;
        }
        public DataTable ConsultaUsuarios(string Login, SqlConnection conn, SqlTransaction tran)
        {
            SqlCommand command = new SqlCommand("ntv_p_sel_tbl_usuarios", conn);
            command.Transaction = tran;
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

        public DataTable ConsultaUsuarioPermissao(string formulario, Int64 id_operacao, Int64 id_usuario, SqlConnection conn, SqlTransaction tran)
        {
            SqlCommand command = new SqlCommand("ntv_p_sel_usuario_formulario_operacao", conn);
            command.Transaction = tran;
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@dc_formulario", formulario));
            command.Parameters.Add(new SqlParameter("@cd_usuario", id_usuario));
            command.Parameters.Add(new SqlParameter("@cd_operacao", id_operacao));

            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_retorno.Load(reader);
            }

            return dtt_retorno;

        }

        public DataTable ConsultaAcesso(Int64 id, SqlConnection conn, SqlTransaction tran)
        {
            SqlCommand command = new SqlCommand("ntv_p_sel_tbl_token_acesso", conn);
            command.Transaction = tran;
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@id_usuario", id));
            command.Parameters.Add(new SqlParameter("@str_usuario", ""));
            command.Parameters.Add(new SqlParameter("@str_nome", ""));


            DataTable dtt_retorno = new DataTable();

            using (var reader = command.ExecuteReader())
            {
                dtt_retorno.Load(reader);
            }

            return dtt_retorno;

        }
        private bool InsAcesso(string Dados, SqlConnection conn, SqlTransaction tran)
        {
            bool bol_retorno = false;

            try
            {
                SqlCommand command = conn.CreateCommand();
                SqlParameter param_id = new SqlParameter();
                command.Transaction = tran;
                command.CommandText = "ntv_p_man_tbl_token_acesso";
                command.CommandType = System.Data.CommandType.StoredProcedure;

                command.Parameters.Add(new SqlParameter("@Tp_processo", "I"));
                command.Parameters.Add(new SqlParameter("@Dados", Dados));
                param_id.ParameterName = "@Novos_id";
                param_id.Size = 1000;
                param_id.Direction = ParameterDirection.Output;
                command.Parameters.Add(param_id);

                command.ExecuteNonQuery();

                bol_retorno = true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return bol_retorno;
        }

        private string Criptografar(string Texto)
        {
            try
            {
                return ExecutarCodificador(Texto, true);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private string Descriptografar(string Texto)
        {
            try
            {
                return ExecutarCodificador(Texto, false);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private static string ExecutarCodificador(string Texto, bool Criptografar)
        {
            string sRetorno = "";

            try
            {
                string str_chave = "Gemco Creative Software";
                int iChaveIndex = 0;
                int iChaveTamanho = 23;
                int iFator1 = 95;
                int iFator2 = 5;

                int[] iChaveCaractere = new int[iChaveTamanho];

                for (int i = 0; i < iChaveTamanho; i++)
                {
                    iChaveCaractere[i] = Convert.ToInt32(Convert.ToChar(str_chave.Substring(i, 1)));
                }

                int iCount = 1;

                for (int i = 0; i < Texto.Length; i++)
                {
                    iChaveIndex++;

                    int iCaracter = Convert.ToInt32(Convert.ToChar(Texto.Substring(i, 1)));
                    int iCaracterNovo = iCaracter ^ iChaveCaractere[iChaveIndex - 1] ^ iFator1 ^ (((iCount - 1) / iFator2) % 255);
                    sRetorno += (char)iCaracterNovo;
                    iCount++;

                    if (Criptografar)
                    {
                        iFator1 = iCaracterNovo;
                    }
                    else
                    {
                        iFator1 = iCaracter;
                    }

                    if (iChaveIndex >= iChaveTamanho)
                    {
                        iChaveIndex = 0;
                    }
                }

                if (Criptografar)
                {
                    if (sRetorno.Length != Texto.Length)
                    {
                        sRetorno = "";
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return sRetorno;
        }
        private StringBuilder SaveThroughXML(DataTable DtTabela, string NomeTabela)
        {
            StringBuilder sb = new System.Text.StringBuilder(1000);
            StringWriter sw = new System.IO.StringWriter(sb);

            DtTabela.TableName = NomeTabela;
            foreach (DataColumn col in DtTabela.Columns)
            {
                col.ColumnMapping = System.Data.MappingType.Attribute;
                if (col.DataType == typeof(DateTime))
                {
                    col.DateTimeMode = DataSetDateTime.Unspecified;
                }
            }

            DtTabela.WriteXml(sw, System.Data.XmlWriteMode.WriteSchema);
            return sb;
        }

    }
}
