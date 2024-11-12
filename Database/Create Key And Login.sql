CREATE ASYMMETRIC KEY [WebSqlKey]
AUTHORIZATION dbo
FROM FILE = 'C:\Users\jason\source\repos\WebSQL\WebSQL\WebSQL-Key.snk' -- Path to Key (Found in the DeviceSQL Project Folder)
CREATE LOGIN [WebSqlClrLogin] FROM ASYMMETRIC KEY [WebSqlKey]
GRANT UNSAFE ASSEMBLY TO [WebSqlClrLogin]