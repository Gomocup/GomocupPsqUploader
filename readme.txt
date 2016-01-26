Program Gomocup Psq Uploader
---------------------------------
A tool for uploading psq files from piskvork client to the server during tournament so the online board is presented in Gomocup online



This program is for uploading small and changing files immediatelly after the change of the content of the file.
In consequence it creates the copy of the directory on server in realtime.

The primary purspose of this project is transfer files to the server during Gomocup tournament (gomocup.org).

During Gomocup, this program is executed on every client station and it is transfering *.psq files to the server, 
where these files are parsed and presented in html.

The program is executed from commandline with 7 parametres:

1. Local directory from which are files uploaded.
2. The mask for files being uploaded.
3. Adress of remote server (it can be address of HTTP uploader or address of FTP server)
4. username for FTP server. It aplies only for FTP server.
5. password for FTP server. It aplies only for FTP server.
6. Remote directory
7. Timeout for file transfer in miliseconds.

Example:

ftpUpload.exe clientAIDir *.psq ftp_server_name username password gomoku/gomocup2008/final/ 30000

Notes:
Only files which MD5 checksum was changed are transferred.

File versioning:
Files on the server side are named according to following pattern: filename(version).ext

At the beggining, there is version number 1.

If the file content is appended, the version is keept same. If the content is ovewrite, the version is increased.

The files in subdirectories are watched as well

Tomas Kubes, 8th of April 2008