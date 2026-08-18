using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIX.Models
{
    public class CriptografiaNtv
    {
        public CriptografiaNtv()
        {

        }

        public string Criptografar(string Texto)
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

        public string Descriptografar(string Texto)
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

    }
}
