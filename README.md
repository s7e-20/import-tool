# Annuity import

Web application that reads one worksheet from an `.xlsx` file and inserts the mapped values into the existing SQL Server table `dbo.CASE_TRANSFERT`.

The application has three screens:

1. **Create new mapping** — upload Excel, assign source columns or constants, test the rows without saving, and export the mapping as JSON.
2. **Edit mapping** — upload the original Excel file and an existing mapping JSON, change the assignments, and export the updated JSON.
3. **Execute a load** — upload Excel plus a saved mapping JSON and insert the rows.

Running a load again inserts the rows again. There is no duplicate check.

## Requirements

- Windows with SQL Server (Developer edition is enough)
- .NET SDK 10
- Node.js, only for building the Vue client

## Database

The default connection string in `src/AnnuityImport.Server/appsettings.json` is:

```text
Server=localhost;Database=AnnuityImport;Trusted_Connection=True;TrustServerCertificate=True
```

On startup the app creates the `AnnuityImport` database and `dbo.CASE_TRANSFERT` when that table is missing. It does not alter or drop an existing table. The creation script is `sql/001-create-case-transfert.sql`.

To use a database that already contains `dbo.CASE_TRANSFERT`, change the connection string and set `Database:EnsureSchema` to `false`.

## Build and run

From this folder:

```powershell
cd client
npm install
npm run build
cd ..
dotnet run --project src/AnnuityImport.Server
```

Open http://localhost:5080

`npm run build` writes the Vue app into the server `wwwroot` folder. After that, running the server does not need Node.js.

## Publish

```powershell
dotnet publish src/AnnuityImport.Server -c Release -o publish
dotnet publish\AnnuityImport.Server.dll
```

Build the Vue client before publishing so `wwwroot` is included.
