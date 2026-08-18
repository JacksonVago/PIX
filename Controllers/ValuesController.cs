using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace PIX.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        // GET api/values
        [HttpGet]
        public ActionResult<string> Get()
        {
            string str_retorno = "";

            using (MemoryStream ms = new MemoryStream())
            {
                QRCodeGenerator qrcodegen = new QRCodeGenerator();
                QRCodeData qrcodedata = qrcodegen.CreateQrCode("Pagamento Lojas Guiabim \nPedido 12345689 \nData de Vencimento : 10/11/2020 \nValor : R$ 1.250,36", QRCodeGenerator.ECCLevel.L);
                QRCode qrcode = new QRCode(qrcodedata);
                using (Bitmap bitmap = qrcode.GetGraphic(10))
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    str_retorno = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                }

            }
            return str_retorno;
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public ActionResult<string> Get(int id)
        {
            return "value";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
