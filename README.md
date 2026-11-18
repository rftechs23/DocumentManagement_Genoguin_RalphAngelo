# Document Management API

A small REST API for storing and retrieving documents while keeping file bytes and searchable metadata in separate storage systems. The API persists metadata in SQL Server and writes file contents to a configured filesystem directory. It uses explicit service and repository layers so HTTP behavior, document workflows, and persistence remain easy to follow and change independently.

## Design

The request path follows the agreed layered flow:

```text
HTTP client -> DocumentsController -> IDocumentService
                                          |-> IDocumentRepository -> DocumentDbContext -> SQL Server
                                          |-> IFileStorage ---------------------------> local filesystem
```

The controller handles multipart binding, status codes, and response formatting. The application service validates uploads and coordinates file and metadata operations. The repository owns document queries and persistence through EF Core. Infrastructure supplies the SQL Server repository and filesystem storage implementations.

API responses use DTOs instead of returning EF entities. They expose the document ID, original filename, content type, size, and upload timestamp; storage identifiers and filesystem paths stay internal. File bytes are returned only by the download endpoint. The schema is managed with EF Core migrations.

## Requirements

- .NET 10 SDK
- SQL Server LocalDB for the default Windows development connection
- EF Core command-line tool 10.0.12 for applying migrations

Install the EF command-line tool if it is not already available:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.12
```

## Configure and run

Restore dependencies:

```powershell
dotnet restore
```

The default connection string in `src/DocumentManagement.Api/appsettings.json` targets `(localdb)\MSSQLLocalDB` and uses Windows integrated security. Apply the checked-in migration to create `DocumentManagementDb`:

```powershell
.\scripts\setup-database.cmd
```

To use another SQL Server instance, set `ConnectionStrings__DefaultConnection` in the environment. Keep credentials in local environment configuration or a secrets manager; do not commit them.

Run the API:

```powershell
dotnet run --project src/DocumentManagement.Api/DocumentManagement.Api.csproj
```

With the supplied HTTP launch profile, Swagger UI is available at [http://localhost:5254/swagger](http://localhost:5254/swagger). Swagger is enabled in Development. The default document storage directory is `src/DocumentManagement.Api/App_Data/Documents`; configure it with `DocumentStorage__RootPath`. The directory is ignored by Git.

## Endpoints

| Method | Route | Behavior |
| --- | --- | --- |
| `POST` | `/api/documents` | Upload one multipart file using the form field `file`; returns `201 Created` and metadata. |
| `GET` | `/api/documents` | List document metadata without file contents. |
| `GET` | `/api/documents/{id}` | Retrieve metadata; returns `404` when the ID is unknown. |
| `GET` | `/api/documents/{id}/download` | Download the stored bytes with the original filename and saved content type. |
| `DELETE` | `/api/documents/{id}` | Delete the document; returns `204` or `404`. |

Upload example:

```powershell
curl.exe -X POST http://localhost:5254/api/documents -F "file=@.\sample.txt"
```

Invalid or empty uploads return `400`; files over 10 MiB return `413`. Error responses use Problem Details. A missing metadata record returns `404`. If metadata exists but its physical file is missing, the download route also returns `404` with a distinct error title.

## File validation and storage behavior

The API accepts any non-empty file up to 10 MiB. It does not restrict file extensions, inspect file contents, or scan uploads for malware. The submitted content type is treated as client-provided metadata rather than verified evidence of the file format; invalid values fall back to `application/octet-stream`.

The submitted filename is reduced to a basename for display. It is never used as a storage path. The filesystem adapter creates a random identifier for each file, rejects identifiers that are not in the expected generated format, and writes under the configured root. Uploads are removed if metadata persistence fails. Deletion first stages the file under a temporary `.deleting` directory so it can be restored if deleting the database row fails. SQL Server and the filesystem do not share a transaction; an abrupt failure between writing a file and saving metadata can leave an unreferenced file, and a failure during deletion can leave a staged file for manual cleanup. There is no background orphan-file cleanup task.

## Tests

Run the automated suite with:

```powershell
dotnet test
```

The service tests cover successful upload, metadata retrieval, missing-document behavior, rejection above the size limit, deletion, and cleanup after metadata persistence fails. They use in-memory fakes and do not require SQL Server LocalDB.

## Scope and limitations

- Authentication and authorization are not implemented, as permitted by the assessment.
- Listing is unpaged; this is intended for the assessment's small document collection.
- The API validates size and basic upload inputs but does not scan or parse file contents.
- Database migrations are applied explicitly with the setup script; the API does not alter the schema on startup.
- The automated tests exercise application-service behavior. They do not replace trying the HTTP routes through Swagger against a running LocalDB instance.

## AI-assisted development

OpenAI Codex was used to help break down the assessment, scaffold the layers, implement portions of the API, persistence and storage workflows, create tests, and draft this documentation. I steered the architecture and scope decisions, reviewed the implementation in slices, and ran the build and automated service tests.

The areas I would review most carefully before production use are the coordination between filesystem and database operations during failures, and the real HTTP behavior of multipart uploads. The current tests use in-memory fakes, and the LocalDB migration could not be applied from the isolated development sandbox; I plan to run the setup script and try the endpoints locally through Swagger.
