using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Codeplex.Data;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Using_Fine_Uploader.Controllers
{
    public class PhotosController : ApiController
    {
        /// <summary>
        /// This action is called when the browser supports FileReader
        /// It will return a JSON object to the client.
        /// It will return 'applicaiton/json' as the content type
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionName("FineUpload")]
        public async Task<FineUpload> FineUpload()
        {
            //When you upload a file to Azure via the web role, it will be stored in its
            //temporary storage. You need to understand that this storage is limited 
            //and also is transient if the web role gets destroyed. You need to take the file
            //from there ASAP. We do that in the same call and save it to blob storage.
            var provider = new MultipartFileStreamProvider(Path.GetTempPath());

            var fineUpload = await ProcessData(provider);            

            return fineUpload;
        }

        /// <summary>
        /// This action is called when the browser doesn't support FileReader
        /// It will always return an Ok and it will include the JSON object 
        /// in text/plain content type.
        /// The method is called IE9 because IE9 cannot handle 'application/json' content type with Fine Uploader
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ActionName("FineUploadIe9")]
        public async Task<HttpResponseMessage> FineUploadIe9()
        {            
            var provider = new MultipartFileStreamProvider(Path.GetTempPath());

            var fineUpload = await ProcessData(provider);

            var res = new HttpResponseMessage(HttpStatusCode.OK);

            var classString = DynamicJson.Serialize(fineUpload);
            res.Content = new StringContent(classString, Encoding.UTF8, "text/plain");
            return res;
        }

        /// <summary>
        /// This method will actually save the the file to Azure
        /// </summary>
        /// <param name="provider"></param>
        /// <returns></returns>
        private async Task<FineUpload> ProcessData(MultipartFileStreamProvider provider)
        {
            var fineUpload = new FineUpload();            
            var agent = string.Empty;

            try
            {                                
                await Request.Content.ReadAsMultipartAsync(provider);

                // snipped validation code
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                    CloudConfigurationManager.GetSetting("StorageConnectionString"));

                CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
                var container = blobClient.GetContainerReference(CloudConfigurationManager.GetSetting("AzurePhotoContainer"));

                foreach (var fileData in provider.FileData)
                {
                    var value = fileData.Headers.ContentDisposition;
                    var name = value.Name;
                    var info = new FileInfo(fileData.LocalFileName);
                    //Look for the piece with name qqfile. That's the piece that contains the 
                    //file we want to upload
                    if (name.IndexOf("qqfile", StringComparison.InvariantCultureIgnoreCase) > -1)
                    {
                        var fileName = info.Name;
                        var blob = container.GetBlockBlobReference(fileName + ".jpg");
                        fineUpload.fileName = fileName + ".jpg";
                        using (var filestream = File.OpenRead(fileData.LocalFileName))
                        {
                            blob.UploadFromStream(filestream);
                        }
                    }
                        //Look for the piece wiht the name 'myagent'. That's the parameter we sent from 
                        //the client. The code doesn't do anything with it. I just wanted to show how that works.
                    else if (name.IndexOf("myagent", StringComparison.InvariantCultureIgnoreCase) > -1)
                    {
                        using (var filestream = new StreamReader(fileData.LocalFileName))
                        {
                            agent = filestream.ReadToEnd();
                            fineUpload.echoAgent = agent;
                            //Now do something with the parameter passed
                        }
                    }

                    File.Delete(fileData.LocalFileName);
                }

                fineUpload.success = true;
            }
            catch (Exception e)
            {
                fineUpload.success = false;
                fineUpload.error = e.Message;
            }
                        

            return fineUpload;
        }
    }

    public class FineUpload
    {
        public bool success { get; set; }
        public string error { get; set; }
        public bool preventRetry { get; set; }
        /// <summary>
        /// File Name is an extra parameter we passed back to the client with the file name
        /// </summary>
        public string fileName { get; set; }

        public string echoAgent { get; set; }
    }
}
