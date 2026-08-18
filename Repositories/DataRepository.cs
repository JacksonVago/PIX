using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;

namespace PIX.Repositories
{
    public class DataRepository
    {
        public DataRepository()
        {

        }

        public bool ProcedureUpdate(dynamic param, string procedure, SqlConnection conn, SqlTransaction tran)
        {

            string str_param = "";
            if (param.GetType() != typeof(string))
            {
                str_param = JsonConvert.SerializeObject(param);
            }
            else
            {
                str_param = param;
            }
            DataTable dtt_filtros = JsonConvert.DeserializeObject<DataTable>(str_param);

            try
            {
                SqlCommand command = new SqlCommand(procedure, conn);

                if (tran != null)
                {
                    command.Transaction = tran;
                }
                command.CommandType = System.Data.CommandType.StoredProcedure;
                for (int r = 0; r < dtt_filtros.Rows.Count; r++)
                {
                    command.Parameters.Add(new SqlParameter("@" + dtt_filtros.Rows[r][0].ToString(), (dtt_filtros.Rows[r][2].ToString() == "DateTime" ? Convert.ToDateTime(dtt_filtros.Rows[r][1]).ToString("yyyy-MM-dd hh:mm:dd") : dtt_filtros.Rows[r][1])));
                }

                command.ExecuteNonQuery();

                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return false;
        }

        public bool ProcedureInsertRet(dynamic param, string procedure, SqlConnection conn, SqlTransaction tran)
        {

            string str_param = "";
            if (param.GetType() != typeof(string))
            {
                str_param = JsonConvert.SerializeObject(param);
            }
            else
            {
                str_param = param;
            }
            DataTable dtt_filtros = JsonConvert.DeserializeObject<DataTable>(str_param);

            try
            {
                SqlCommand command = new SqlCommand(procedure, conn);

                if (tran != null)
                {
                    command.Transaction = tran;
                }
                command.CommandType = System.Data.CommandType.StoredProcedure;
                for (int r = 0; r < dtt_filtros.Rows.Count; r++)
                {
                    command.Parameters.Add(new SqlParameter("@" + dtt_filtros.Rows[r][0].ToString(), (dtt_filtros.Rows[r][2].ToString() == "DateTime" ? Convert.ToDateTime(dtt_filtros.Rows[r][1]).ToString("yyyy-MM-dd hh:mm:dd") : dtt_filtros.Rows[r][1])));
                }
                SqlParameter str_retorno = new SqlParameter();
                str_retorno.Direction = ParameterDirection.Output;
                str_retorno.Size = 1000;
                str_retorno.ParameterName = "@Novos_id";
                str_retorno.SqlDbType = SqlDbType.VarChar;
                command.Parameters.Add(str_retorno);

                command.ExecuteNonQuery();

                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return false;
        }

        public string ManutencaoTabela(string Operacao, string dados, string Tabela, SqlConnection conn, SqlTransaction tran)
        {

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
                command.CommandText = "ntv_p_man_" + Tabela.Replace("ntv_","");
                command.CommandType = System.Data.CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@Tp_processo", Operacao));
                command.Parameters.Add(new SqlParameter("@Dados", dados));
                command.Parameters.Add(str_retorno);

                command.ExecuteNonQuery();

                return str_retorno.Value.ToString();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public string ManutencaoTabela<T>(string Operacao, IList<T> dados, string Tabela, SqlConnection conn, SqlTransaction tran)
        {

            DataTable dtt_dados = ToDataTable<T>(dados);
            string str_dadosXml = SaveThroughXML(dtt_dados, Tabela).ToString();

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

                command.ExecuteNonQuery();

                return str_retorno.Value.ToString();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public string ManutencaoTabela<T>(string Operacao, IList<T> dados, string Tabela, SqlConnection conn)
        {

            return ManutencaoTabela<T>(Operacao, dados, Tabela, conn, null);
        }

        public dynamic ConsultaGenerica(dynamic filtros, string procedure, SqlConnection conn, SqlTransaction tran)
        {

            //DataTable dtt_filtros = ToDataTable<dynamic>(filtros);
            string str_param = "";
            if (filtros.GetType() != typeof(string))
            {
                str_param = JsonConvert.SerializeObject(filtros);
            }
            else
            {
                str_param = filtros;
            }
            DataTable dtt_filtros = JsonConvert.DeserializeObject<DataTable>(str_param);
            

            try
            {
                SqlCommand command = new SqlCommand(procedure, conn);

                if (tran != null){
                    command.Transaction = tran;
                }
                command.CommandType = System.Data.CommandType.StoredProcedure;
                for (int r = 0; r < dtt_filtros.Rows.Count; r++)
                {
                    command.Parameters.Add(new SqlParameter("@" + dtt_filtros.Rows[r][0].ToString(), (dtt_filtros.Rows[r][2].ToString() == "DateTime" ? Convert.ToDateTime(dtt_filtros.Rows[r][1]).ToString("yyyy-MM-dd") : dtt_filtros.Rows[r][1])));
                }
                //command.Parameters.Add(new SqlParameter("@EmissaoIni", DataIni.ToString("yyyy-MM-dd")));

                DataTable dtt_retorno = new DataTable();

                using (var reader = command.ExecuteReader())
                {
                    dtt_retorno.Load(reader);
                }

                return JsonConvert.SerializeObject(dtt_retorno);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }



        public dynamic ConsultaGenerica(dynamic filtros, string procedure, SqlConnection conn)
        {

            try
            {
                return ConsultaGenerica(filtros, procedure, conn, null);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public DataTable ConsultaGenericaDtt(dynamic filtros, string procedure, SqlConnection conn)
        {
            try
            {
                return ConsultaGenericaDtt(filtros, procedure, conn, null);

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public DataTable ConsultaGenericaDtt(dynamic filtros, string procedure, SqlConnection conn, SqlTransaction tran)
        {

            //DataTable dtt_filtros = ToDataTable<dynamic>(filtros);
            string str_param = "";
            if (filtros.GetType() != typeof(string))
            {
                str_param = JsonConvert.SerializeObject(filtros);
            }
            else
            {
                str_param = filtros;
            }
            DataTable dtt_filtros = JsonConvert.DeserializeObject<DataTable>(str_param);


            try
            {
                SqlCommand command = new SqlCommand(procedure, conn);

                if (tran != null)
                {
                    command.Transaction = tran;
                }
                command.CommandType = System.Data.CommandType.StoredProcedure;
                for (int r = 0; r < dtt_filtros.Rows.Count; r++)
                {
                    command.Parameters.Add(new SqlParameter("@" + dtt_filtros.Rows[r][0].ToString(), (dtt_filtros.Rows[r][2].ToString() == "DateTime" ? Convert.ToDateTime(dtt_filtros.Rows[r][1]).ToString("yyyy-MM-dd") : dtt_filtros.Rows[r][1])));
                }
                //command.Parameters.Add(new SqlParameter("@EmissaoIni", DataIni.ToString("yyyy-MM-dd")));

                DataTable dtt_retorno = new DataTable();

                using (var reader = command.ExecuteReader())
                {
                    dtt_retorno.Load(reader);
                }

                return dtt_retorno;

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
                    DataColumn col = new DataColumn();

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
                            col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                            col.DataType = typeof(string);
                            col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                            dtt_retorno.Columns.Add(col);
                            //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(string));
                        }
                    }
                    continue;
                Label_0182:
                    if (Convert.ToInt16(dtt_result.Rows[i]["decimal"].ToString()) > 0)
                    {
                        col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                        col.DataType = typeof(double);
                        col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                        dtt_retorno.Columns.Add(col);
                        //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(double));
                    }
                    else if (Convert.ToInt16(dtt_result.Rows[i]["precisao"].ToString()) < 4)
                    {
                        col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                        col.DataType = typeof(Int16);
                        col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                        dtt_retorno.Columns.Add(col);
                        //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int16));
                    }
                    else if (Convert.ToInt16(dtt_result.Rows[i]["precisao"].ToString()) < 6)
                    {
                        col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                        col.DataType = typeof(Int32);
                        col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                        dtt_retorno.Columns.Add(col);
                        //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int32));
                    }
                    else
                    {
                        col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                        col.DataType = typeof(Int64);
                        col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                        dtt_retorno.Columns.Add(col);
                        //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int64));
                    }
                    continue;
                Label_02C7:
                    col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                    col.DataType = typeof(Int64);
                    col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                    dtt_retorno.Columns.Add(col);
                    //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int64));
                    continue;
                Label_02FB:
                    col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                    col.DataType = typeof(Int32);
                    col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                    dtt_retorno.Columns.Add(col);
                    //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int32));
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
                    DataColumn col = new DataColumn();

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
                            col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                            col.DataType = typeof(string);
                            col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                            dtt_retorno.Columns.Add(col);
                            //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(string));

                        }
                    }
                    continue;
                Label_0182:
                    if (Convert.ToInt16(dtt_result.Rows[i]["decimal"].ToString()) > 0)
                    {
                        col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                        col.DataType = typeof(double);
                        col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                        dtt_retorno.Columns.Add(col);
                        //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(double));
                    }
                    else if (Convert.ToInt16(dtt_result.Rows[i]["precisao"].ToString()) < 4)
                    {
                        col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                        col.DataType = typeof(Int16);
                        col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                        dtt_retorno.Columns.Add(col);
                        //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int16));
                    }
                    else if (Convert.ToInt16(dtt_result.Rows[i]["precisao"].ToString()) < 6)
                    {
                        col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                        col.DataType = typeof(Int32);
                        col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                        dtt_retorno.Columns.Add(col);
                        //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int32));
                    }
                    else
                    {
                        col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                        col.DataType = typeof(Int64);
                        col.AllowDBNull = (dtt_result.Rows[i]["coluna"].ToString() == "0" ? false : true);
                        dtt_retorno.Columns.Add(col);
                        //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int64));
                    }
                    continue;
                Label_02C7:
                    col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                    col.DataType = typeof(Int64);
                    col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                    dtt_retorno.Columns.Add(col);
                    //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int64));
                    continue;
                Label_02FB:
                    col.ColumnName = dtt_result.Rows[i]["coluna"].ToString();
                    col.DataType = typeof(Int32);
                    col.AllowDBNull = (dtt_result.Rows[i]["isnullable"].ToString() == "0" ? false : true);
                    dtt_retorno.Columns.Add(col);
                    //dtt_retorno.Columns.Add(dtt_result.Rows[i]["coluna"].ToString(), typeof(Int32));
                }
                return dtt_retorno;
            }
            return dtt_retorno;

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
                            values[i] = (props[i].GetValue(item)== null ? props[i].GetValue(item) : Convert.ToDateTime(props[i].GetValue(item)).ToString("dd/MM/yyyy HH:mm:ss"));
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
            catch(Exception ex)
            {
                throw ex;
            }
            return table;

        }

    }
}
