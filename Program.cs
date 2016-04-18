using System;
using System.Net;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using System.Collections.Specialized;

namespace Gomocup.PsqUploader
{
    public struct FileChangeInfo
    {
        public long length;
        public int count;
        public byte[] hash;
    }



    class Program
    {
        static Dictionary<string, FileChangeInfo> fileDictionary = new Dictionary<string, FileChangeInfo>();
        static MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();

        static string local;
        static string searchPattern;
        static string server;
        static string user;
        static string pass;
        static string remote;
        static int timeout;
        static int sleep;

        const string ftp = "ftp://";
        const string http = "http://";

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                using (OpenFileDialog op = new OpenFileDialog())
                {
                    op.Filter = "config *.xml|*.xml";
                    if (op.ShowDialog() != DialogResult.OK)
                        return;
                    ParseConfig(op.FileName);
                }

            }
            else if (args.Length == 1)
            {
                ParseConfig(args[0]);
            }
            else if (args.Length < 7)
            {
                Console.WriteLine("few parametres....");
                Console.WriteLine("local searchPattern server user pass remote timeout");
                return;
            }
            else if (args.Length > 7)
            {
                Console.WriteLine("too many parametres....");
                Console.WriteLine("local searchPattern server user pass remote timeout");
                local = args[0];
                searchPattern = args[1];
                server = args[2];
                user = args[3];
                pass = args[4];
                remote = args[5];
                timeout = int.Parse(args[6]);
            }
            else
            {
                local = args[0];
                searchPattern = args[1];
                server = args[2];
                user = args[3];
                pass = args[4];
                remote = args[5];
                timeout = int.Parse(args[6]);
            }



            if (server.StartsWith(ftp))
            {
                remote = remote.TrimEnd(new char[] { '/', '\\' });
            }

            if (!Directory.Exists(local))
            {
                Console.WriteLine("directory " + local + "doesn't exists");
                return;
            }
            FileSystemWatcher watcher = new FileSystemWatcher(local, searchPattern);
            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
            watcher.Created += new FileSystemEventHandler(watcher_upload);
            watcher.Changed += new FileSystemEventHandler(watcher_upload);
            watcher.IncludeSubdirectories = false;
            Console.WriteLine("Waiting for changes in directory " + local);
            Console.WriteLine("Search pattern is set to " + searchPattern);
            watcher.EnableRaisingEvents = true;
            Console.ReadKey();
        }

        private static void ParseConfig(string path)
        {
            XDocument xdoc = XDocument.Load(path);

            var root = xdoc.Descendants("uploadconfiguration");

            local = root.Descendants("localpath").FirstOrDefault().Value;
            searchPattern = root.Descendants("searchpattern").FirstOrDefault().Value;
            server = root.Descendants("host").FirstOrDefault().Value;
            timeout = int.Parse(root.Descendants("timeout").FirstOrDefault().Value);
            user = root.Descendants("username").FirstOrDefault().Value;
            pass = root.Descendants("password").FirstOrDefault().Value; ;
            remote = root.Descendants("remotepath").FirstOrDefault().Value; ;
        }

        public static void Upload(byte[] localContent, string localPath, string localFileName)
        {
            if (server.StartsWith(ftp))
            {
                Uri uri = new Uri(server + "/" + remote + "/" + localFileName);
                UploadFileOverFtp(localContent, localPath, uri);
            }
            else if (server.StartsWith(http))
            {
                UploadHttp(localContent, localPath);
            }
        }

        private static void UploadHttp(byte[] localContent, string localPath)
        {
            int pos = server.LastIndexOf("?");
            string query = server.Substring(pos + 1);

            NameValueCollection nvc = new NameValueCollection();
            string url = server.Substring(0, pos);
            string filename = Path.GetFileName(localPath);
            HttpUploadFile(server, localContent, filename, "file", "application/octet-stream", nvc);

            Console.WriteLine(DateTime.Now + " uploaded " + localPath);
        }

        public static void HttpUploadFile(string url, byte[] filebytes, string filename, string paramName, string contentType, NameValueCollection nvc)
        {
            Console.WriteLine(string.Format("Uploading {0} to {1}", filename, url));
            string boundary = "----------------------------" + DateTime.Now.Ticks.ToString("x");
            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            HttpWebRequest wr = (HttpWebRequest)WebRequest.Create(url);
            wr.ContentType = "multipart/form-data; boundary=" + boundary;
            wr.Method = "POST";
            wr.Timeout = timeout;
            wr.KeepAlive = false;
            wr.Credentials = System.Net.CredentialCache.DefaultCredentials;

            Stream rs = wr.GetRequestStream();

            string formdataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}";
            foreach (string key in nvc.Keys)
            {
                rs.Write(boundarybytes, 0, boundarybytes.Length);
                string formitem = string.Format(formdataTemplate, key, nvc[key]);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                rs.Write(formitembytes, 0, formitembytes.Length);
            }
            rs.Write(boundarybytes, 0, boundarybytes.Length);

            string headerTemplate = "Content-Disposition: form-data; name=\"{0}\";filename=\"{1}\"\r\n Content-Type: {2}\r\n\r\n";
            string header = string.Format(headerTemplate, paramName, filename, contentType);
            byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
            rs.Write(headerbytes, 0, headerbytes.Length);

            rs.Write(filebytes, 0, filebytes.Length);

            byte[] trailer = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "--\r\n");
            rs.Write(trailer, 0, trailer.Length);
            rs.Close();

            WebResponse wresp = null;
            try
            {
                wresp = wr.GetResponse();
                using (Stream stream2 = wresp.GetResponseStream())
                {
                    //StreamReader reader2 = new StreamReader(stream2);
                    //Console.WriteLine(string.Format("File uploaded, server response is: {0}", reader2.ReadToEnd()));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error uploading file {0}", ex);                
            }
            finally
            {
                if (wresp != null)
                {
                    wresp.Close();
                }
            }
        }

           
        public static void UploadFileOverFtp(byte[] localFileContent, string localFilePath, Uri remotePath)
        {
            Stream strm = null;
            try //musi byt dva try-catch, aby pad na prvnim souboru, neznemoznil upload druheho
            {
                FtpWebRequest reqFTP = (FtpWebRequest)FtpWebRequest.Create(remotePath);
                reqFTP.Credentials = new NetworkCredential(user, pass);
                reqFTP.Method = WebRequestMethods.Ftp.UploadFile;
                reqFTP.UseBinary = true;
                reqFTP.KeepAlive = false;
                reqFTP.Timeout = timeout;

                strm = reqFTP.GetRequestStream();
                reqFTP.ContentLength = localFileContent.Length;
                strm.Write(localFileContent, 0, localFileContent.Length);
                strm.Close();
                Console.WriteLine(DateTime.Now + " upload " + localFilePath + " to " + remotePath.ToString() + " OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now + " " + ex.Message, "Upload Error");
            }
            finally
            {
                if (strm != null)
                {
                    strm.Close();
                }
            }
        }

        static bool ArrayEquals<T>(T[] arr1, T[] arr2)
            where T : IComparable
        {
            if (arr1.Length != arr2.Length)
                return false;
            for (int i = 0; i < arr1.Length; i++)
            {
                if (arr1[i].CompareTo(arr2[i]) != 0)
                {
                    return false;
                }
            }
            return true;
        }

        static void watcher_upload(object sender, FileSystemEventArgs e)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(e.FullPath);

                //suitable only for small files which fit the buffer
                Thread.Sleep(100); //let the executing process close the file



                byte[] content = null;

                string mutexName = "Global\\" + e.FullPath.Replace('\\', ':').ToLower();
                //Console.WriteLine("mutex: " + mutexName);

                //globalni mutex is shared with piskvork app
                using (var mutex = new Mutex(false, mutexName))
                {
                    try
                    {
                        if (!mutex.WaitOne(TimeSpan.FromMilliseconds(500), false))
                        {
                            Console.WriteLine("mutex timeout: " + mutexName);
                            return; //mutex was not realase, dont upload nothing
                        }
                    }
                    catch (AbandonedMutexException)
                    {
                        // Log the fact the mutex was abandoned in another process, it will still get aquired
                    }

                    try
                    {
                        content = File.ReadAllBytes(e.FullPath);
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }
                }

                byte[] hash = md5.ComputeHash(content);

                FileChangeInfo lc;
                lc.count = 1;
                if (fileDictionary.ContainsKey(e.FullPath))
                {
                    lc = fileDictionary[e.FullPath];

                    if (fileInfo.Length < lc.length)
                    {
                        //the wile was changed, it seems it is a new file, because it is smaller then last time
                        lc.count++;
                    }
                    else if (!ArrayEquals(lc.hash, md5.ComputeHash(content, 0, (int)lc.length)))
                    {
                        // file was changed (content is not appedned), because MD5 checksum of its old part is same
                        lc.count++;
                    }
                    else if (fileInfo.Length == lc.length)
                    {
                        return; //file is same, filewatcher send more events for one file change
                    }
                }
                lc.length = fileInfo.Length;
                lc.hash = hash;

                string extension = Path.GetExtension(e.FullPath);
                string remoteFileName = String.Format("{0}{2}", Path.GetFileNameWithoutExtension(e.FullPath), lc.count, extension);

                Upload(content, e.FullPath, remoteFileName);

                fileDictionary[e.FullPath] = lc;
            }
            catch (Exception ex)
            {
                Console.WriteLine(DateTime.Now + " " + ex.Message, "Read Error");
            }

        }
    }
}
