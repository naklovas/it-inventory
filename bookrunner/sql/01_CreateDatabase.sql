/* ===========================================================================
   BookRunner - veritabani kurulumu

   CALISTIRMADAN ONCE OKUYUN
   -------------------------
   Script iki bolumden olusur:

     BOLUM 1  Veritabani, sema ve ayarlar.  Herkes icin ayni, duzenleme
              gerektirmez.
     BOLUM 2  Uygulama hesabinin yetkilendirilmesi.  Bu bolumdeki
              @appAccount degerini KENDI ORTAMINIZA GORE DUZENLEYIN.

   @appAccount, uygulamanin uzerinde calisacagi Windows hesabidir:
     - IIS'te barindiriyorsaniz uygulama havuzunun kimligi
       (orn. 'IIS APPPOOL\BookRunner' veya 'CONTOSO\svc-bookrunner')
     - Windows servisi olarak calistiriyorsaniz servisin oturum hesabi
     - Kendi makinenizde deneme yapiyorsaniz kendi hesabiniz
       (orn. 'CONTOSO\visikhan')

   Deger duzenlenmezse BOLUM 2 atlanir ve script hata vermeden biter;
   veritabani yine olusur.

   Calistirma:  sqlcmd -S <sunucu> -i 01_CreateDatabase.sql
   Gereken yetki: sysadmin (veya dbcreator + securityadmin)
   =========================================================================== */

/* ---------------------------------------------------------------------------
   BOLUM 1 - Veritabani ve sema
   --------------------------------------------------------------------------- */

IF DB_ID(N'BookRunner') IS NULL
BEGIN
    PRINT N'BookRunner veritabani olusturuluyor...';
    CREATE DATABASE [BookRunner];
END
ELSE
BEGIN
    PRINT N'BookRunner veritabani zaten var; olusturma atlandi.';
END
GO

/* Okuma sorgulari yazma islemlerini beklemesin diye anlik goruntu izolasyonu.
   Yetki yetmezse kurulum durmaz; yalnizca uyari verilir. */
BEGIN TRY
    ALTER DATABASE [BookRunner] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
    PRINT N'READ_COMMITTED_SNAPSHOT acildi.';
END TRY
BEGIN CATCH
    PRINT N'UYARI: READ_COMMITTED_SNAPSHOT acilamadi -> ' + ERROR_MESSAGE();
    PRINT N'       Kurulum devam ediyor. Bu ayari sonradan da acabilirsiniz.';
END CATCH
GO

USE [BookRunner];
GO

/* Uygulama semasi. EF Core migration'lari bu sema altinda calisir. */
IF SCHEMA_ID(N'bookrunner') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA [bookrunner]');
    PRINT N'bookrunner semasi olusturuldu.';
END
ELSE
BEGIN
    PRINT N'bookrunner semasi zaten var.';
END
GO

/* ---------------------------------------------------------------------------
   BOLUM 2 - Uygulama hesabi

   >>> ASAGIDAKI SATIRI DUZENLEYIN <<<
   --------------------------------------------------------------------------- */

DECLARE @appAccount sysname = N'CONTOSO\svc-bookrunner';   -- <<< DEGISTIRIN

/* --------------------------- buradan asagisi degistirilmeden calisir ------ */

DECLARE @placeholder sysname = N'CONTOSO\svc-bookrunner';
DECLARE @sql nvarchar(max);

IF @appAccount = @placeholder
BEGIN
    PRINT N'';
    PRINT N'-------------------------------------------------------------------';
    PRINT N'BOLUM 2 ATLANDI: @appAccount hala ornek deger.';
    PRINT N'';
    PRINT N'Veritabani hazir, ancak uygulamanin baglanabilmesi icin calisacagi';
    PRINT N'Windows hesabina yetki verilmesi gerekir. Script''in basindaki';
    PRINT N'@appAccount satirini kendi hesabinizla degistirip tekrar calistirin.';
    PRINT N'';
    PRINT N'Kendi hesabinizi ogrenmek icin (PowerShell):   whoami';
    PRINT N'-------------------------------------------------------------------';
END
ELSE
BEGIN
    BEGIN TRY
        /* Sunucu oturumu */
        IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @appAccount)
        BEGIN
            SET @sql = N'CREATE LOGIN ' + QUOTENAME(@appAccount) + N' FROM WINDOWS';
            EXEC sp_executesql @sql;
            PRINT N'Login olusturuldu: ' + @appAccount;
        END
        ELSE
        BEGIN
            PRINT N'Login zaten var: ' + @appAccount;
        END

        /* Veritabani kullanicisi */
        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @appAccount)
        BEGIN
            SET @sql = N'CREATE USER ' + QUOTENAME(@appAccount) + N' FOR LOGIN ' + QUOTENAME(@appAccount);
            EXEC sp_executesql @sql;
            PRINT N'Veritabani kullanicisi olusturuldu.';
        END

        /* Uygulama kendi semasinda okur/yazar.
           db_ddladmin yalnizca Database:MigrateOnStartup = true iken gereklidir;
           semayi elle yonetiyorsaniz asagidaki satiri kaldirabilirsiniz. */
        SET @sql =
            N'ALTER ROLE [db_datareader] ADD MEMBER ' + QUOTENAME(@appAccount) + N';' +
            N'ALTER ROLE [db_datawriter] ADD MEMBER ' + QUOTENAME(@appAccount) + N';' +
            N'ALTER ROLE [db_ddladmin]   ADD MEMBER ' + QUOTENAME(@appAccount) + N';';
        EXEC sp_executesql @sql;

        PRINT N'Roller verildi: db_datareader, db_datawriter, db_ddladmin';
    END TRY
    BEGIN CATCH
        PRINT N'';
        PRINT N'-------------------------------------------------------------------';
        PRINT N'BOLUM 2 BASARISIZ (veritabani yine de olusturuldu).';
        PRINT N'Hata ' + CAST(ERROR_NUMBER() AS nvarchar(10)) + N': ' + ERROR_MESSAGE();
        PRINT N'';

        IF ERROR_NUMBER() IN (15401, 15007)
        BEGIN
            PRINT N'Bu hata, "' + @appAccount + N'" hesabinin Active Directory''de';
            PRINT N'bulunamadigi anlamina gelir. Kontrol edin:';
            PRINT N'  - Etki alani adi dogru mu? (NetBIOS adi kullanin: CONTOSO\kullanici)';
            PRINT N'  - Hesap gercekten var mi?   (PowerShell: Get-ADUser svc-bookrunner)';
            PRINT N'  - Deneme yapiyorsaniz kendi hesabinizi yazabilirsiniz (whoami)';
        END
        ELSE IF ERROR_NUMBER() IN (15247, 262, 300)
        BEGIN
            PRINT N'Bu hata yetki eksikligini gosterir. Login olusturmak icin';
            PRINT N'sysadmin veya securityadmin rolu gerekir. SQL yoneticinizden';
            PRINT N'BOLUM 2''yi calistirmasini isteyin.';
        END

        PRINT N'-------------------------------------------------------------------';
    END CATCH
END
GO

PRINT N'';
PRINT N'Sonraki adim: 02_BookRunner_Schema.sql';
PRINT N'  sqlcmd -S <sunucu> -d BookRunner -i 02_BookRunner_Schema.sql';
GO
