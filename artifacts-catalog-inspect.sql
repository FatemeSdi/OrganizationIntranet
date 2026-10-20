SELECT ApplicationId,ApplicationCode,ApplicationName,IsActive,IsPublic FROM App.Application;
SELECT UserId,Username,IsActive FROM Sec.[User] WHERE Username=N'fatemeh.sadeghi';
SELECT RoleId,RoleCode,RoleName,IsActive FROM Sec.Role WHERE RoleCode='EXPERT';
SELECT ur.* FROM Sec.UserRole ur JOIN Sec.[User] u ON u.UserId=ur.UserId WHERE u.Username=N'fatemeh.sadeghi';
SELECT COLUMN_NAME,IS_NULLABLE,COLUMN_DEFAULT FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA='App' AND TABLE_NAME='Application';
