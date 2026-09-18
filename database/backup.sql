SET NOCOUNT ON;
DECLARE @directory nvarchar(4000)=CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'));
IF @directory IS NULL THROW 51000,'Backup directory unavailable.',1;
DECLARE @path nvarchar(4000)=@directory+CASE WHEN RIGHT(@directory,1) IN ('\','/') THEN '' ELSE '\' END+DB_NAME()+'_BeforeDatabaseReview_'+CONVERT(char(8),GETDATE(),112)+'_'+REPLACE(CONVERT(char(8),GETDATE(),108),':','')+'.bak';
DECLARE @database sysname=DB_NAME();
BACKUP DATABASE @database TO DISK=@path WITH COPY_ONLY,CHECKSUM;
RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;
SELECT @path AS VerifiedBackupPath;
