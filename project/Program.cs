using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace FTPBackupAgente
{
    class Arvore
    {
        public string Dir;
        public List<Arvore> DirFilhos = new List<Arvore>();
    }

    class Program
    {
        static FtpWebRequest request;
        static string host;
        static Arvore arvoreDirLocal = new Arvore();
        static NetworkCredential Credencial;
        static List<string> localFiles = new List<string>();
        static StreamWriter log;

        /// <summary>
        /// os argumentos
        /// 1º Path da pasta a ser ser copiada (incluindo tudo dentro recursivamente). Ex: c:\site\upload
        /// 2º host. Ex: ftp://heab-nas/
        /// 2º usuário do FTP
        /// 3º senha do usuário
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // DEBUG
            // args = new string[4] {@"D:\VisualStudioCode\LibMan", "ftp://172.17.10.56/Test", "FTPUser", "1qaz@Wsx" };
            
            if (args.Length != 4)
            {
                Console.WriteLine("Especifique 4 parâmetros: [Nome do diretéorio a ser copiado] [host] [Usuario FTP] [Senha FTP]");
                Environment.Exit(0);
            }

            string rootPath = args[0];

            
            log = File.CreateText(Directory.GetCurrentDirectory() + "\\BackupToFTPCMD.log");

            WriteLog("Backup Iniciado");
            // corrige o path para sempre terminar com \
            rootPath = CorrigirTerminacaoDir(rootPath);
            var fileInfo = new FileInfo(rootPath);
            // corrige o nome do host para sempre terminar com /
            host = args[1];
            host = host.EndsWith("/") ? host : host + "/";

            arvoreDirLocal.Dir = fileInfo.Directory.Name;
            GetDirsAndFilesLocal(rootPath, arvoreDirLocal);

            Credencial = new NetworkCredential(args[2], args[3]);
            CreateRemoteDirs(arvoreDirLocal, "");
            UploadArquivos();
            WriteLog("Quantidade de arquivos copiados: " +  localFiles.Count.ToString());
            Console.WriteLine("O arquivo de log backup.log foi escrito na pasta corrente");

            log.Close();
        }

        static void WriteLog(string texto)
        {
            log.WriteLine(DateTime.Now.ToString() + " " + texto);
        }

        static string CorrigirTerminacaoDir(string dir)
        {
            if (!dir.EndsWith("\\"))
            {
                return dir + "\\";
            }
            else
            {
                return dir;
            }
        }

        static void GetDirsAndFilesLocal(string path, Arvore argArvoreDirLocal)
        {
            var dirs = Directory.GetDirectories(path);
            foreach (var item in dirs)
            {
                var fileInfo = new FileInfo(CorrigirTerminacaoDir(item));
                argArvoreDirLocal.DirFilhos.Add(new Arvore() { Dir = fileInfo.Directory.Name });
                GetDirsAndFilesLocal(item, argArvoreDirLocal.DirFilhos[argArvoreDirLocal.DirFilhos.Count - 1]);
            }
            GetFilesLocal(path);
        }

        static void CreateRemoteDirs(Arvore argArvoreDirLocal, string dirPai)
        {
            string url = host + dirPai + argArvoreDirLocal.Dir;
 
            WriteLog("Criando diretório: " + url);
            request = (FtpWebRequest)WebRequest.Create(url);
            request.Credentials = Credencial;
            request.Method = WebRequestMethods.Ftp.MakeDirectory;
            try
            {
                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                WriteLog("Resposta do servidor, status: " + response.StatusDescription);
                response.Close();
            }
            catch (WebException e)
            {
                WriteLog("Falha ao tentar criar diretório, talvez ele já exista, " + e.Message);
                log.WriteLine("");
            }

            foreach (var item in argArvoreDirLocal.DirFilhos)
            {
                CreateRemoteDirs(item, dirPai + argArvoreDirLocal.Dir + "/");
            }
        }

        static void UploadArquivos()
        {
            foreach (var item in localFiles)
            {
                WriteLog("Enviando arquivo: " + item);
                Console.WriteLine("Enviando arquivo: " + item);

                byte[] fileContents = File.ReadAllBytes(item);
                int onde = item.IndexOf(arvoreDirLocal.Dir);
                string path = item.Substring(onde + arvoreDirLocal.Dir.Length).Replace("\\", "/");
                
                try
                {
                    request = (FtpWebRequest)WebRequest.Create(host + arvoreDirLocal.Dir + path);
                    request.UseBinary = true;
                    request.Credentials = Credencial;
                    request.Method = WebRequestMethods.Ftp.UploadFile;

                    request.ContentLength = fileContents.Length;
                    Stream requestStream = request.GetRequestStream();
                    requestStream.Write(fileContents, 0, fileContents.Length);
                    requestStream.Close();

                    FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                    WriteLog("Resposta do servidor: " + response.StatusDescription);
                    response.Close();
                }
                catch (WebException e)
                {
                    WriteLog("Ao tentar fazer upload diretório, " + item + ", " + e.Message);
                }

            }
        }

        static void GetFilesLocal(string path)
        {
            var files = Directory.GetFiles(path);
            foreach (var item in files)
            {
                if (!item.EndsWith("Thumbs.db")) // não copia o Thumbs.db (arquivo de cache de miniaturas de imagens da pasta)
                    localFiles.Add(item);
            }
        }
    }
}
