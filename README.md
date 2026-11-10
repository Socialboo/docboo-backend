# docboo-backend

API do **Docboo**, o formatador de trabalhos acadêmicos (MVP: ABNT para TCC/monografia). O aluno monta o trabalho no editor do [docboo-frontend](https://github.com/Socialboo/docboo-frontend), e a API guarda o documento e gera os arquivos `.docx` e `.pdf` formatados.

**Stack:**
- ASP.NET Core (.NET 10) com Controllers;
- EF Core + PostgreSQL 18;
- Open XML SDK para gerar o `.docx`;
- LibreOffice headless para converter em PDF;
- OpenAPI com Swagger UI e Scalar.

## Estrutura

| Projeto | Responsabilidade |
| --- | --- |
| `Backend/` | Host HTTP, Controllers, OpenAPI (`Backend/openapi/openapi.json`, gerado no build) |
| `DTOs/` | Contratos HTTP |
| `Domain/` | Entidades e modelo de documento (`Domain/Documents`) |
| `Application/` | Casos de uso, saneamento e validação, portas (`ICurrentUser`, `IWorkRepository`, …) |
| `Infrastructure/` | EF Core/Postgres, migrations, usuário local |
| `Exporter/` | Perfis de norma (`Exporter/Profiles/*.json`) e geração de documentos |
| `tests/` | `Application.Tests`, `Exporter.Tests`, `IntegrationTests` (Testcontainers) |
| `docs/` | ADRs, modelo de documento, regras ABNT e documento de exemplo "golden" |

## Subir com Docker

Pré-requisito: Docker com Compose v2.

```bash
cp .env.example .env
```

```bash
docker compose up --build -d
```

```bash
docker compose ps
```

Os três serviços (`db`, `pgadmin`, `api`) devem ficar `healthy`. Depois disso:

| URL | O quê |
| --- | --- |
| http://localhost:5248/health | Healthcheck da API (inclui o Postgres) |
| http://localhost:5248/scalar | Referência da API (Scalar) |
| http://localhost:5248/swagger | Swagger UI |
| http://localhost:5248/openapi/v1.json | Documento OpenAPI |
| http://localhost:5050 | pgAdmin |
| `127.0.0.1:5432` | PostgreSQL 18 |

### pgAdmin

- O pgAdmin abre sem tela de login. Ele roda em modo desktop e só escuta em `127.0.0.1`.
- O servidor **docboo (docker)** já vem cadastrado (`docker/pgadmin/servers.json`).
- No primeiro acesso, o pgAdmin pede a senha do banco (`POSTGRES_PASSWORD`). Marque "Save password".
- Se você mudar `POSTGRES_USER` ou `POSTGRES_DB`, ajuste também o `servers.json`.

### Hot reload (API em container com `dotnet watch`)

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

## Desenvolvimento nativo

Pré-requisitos: .NET SDK 10 e Docker (só para o Postgres).

```bash
docker compose up -d db pgadmin
```

```bash
dotnet run --project Backend --launch-profile http
```

A API sobe em http://localhost:5248 e aplica as migrations na inicialização.

## Frontend

O [docboo-frontend](https://github.com/Socialboo/docboo-frontend) acessa esta API por proxy de `/api`. A API precisa estar escutando em `localhost:5248`, seja em Docker ou nativa. O front gera os tipos TypeScript a partir de `/openapi/v1.json`.

## API (MVP)

| Método | Rota | Descrição |
| --- | --- | --- |
| GET | `/api/health` | Status da API e do banco |
| GET | `/api/profiles` | Perfis de norma disponíveis |
| GET | `/api/works` | Trabalhos do usuário (mais recentes primeiro) |
| POST | `/api/works` | Cria um trabalho `{ profileId, title? }` com documento vazio |
| GET | `/api/works/{id}` | Trabalho com o documento |
| PUT | `/api/works/{id}/document` | Salva `{ version, document }`. Responde 409 se a versão estiver desatualizada, 400 com erros por caminho ou 413 acima de 4 MiB |
| DELETE | `/api/works/{id}` | Remove o trabalho |

O modelo de documento está descrito em [docs/document-model.md](docs/document-model.md).

## Banco e migrations

```bash
dotnet ef migrations add NomeDaMigration --project Infrastructure --startup-project Infrastructure --output-dir Persistence/Migrations
```

Para desativar a migração automática na inicialização, use `Database__MigrateOnStartup=false`.

## Testes

```bash
dotnet test docboo-backend.slnx
```

- `IntegrationTests` sobe um PostgreSQL 18 descartável com Testcontainers, por isso **precisa do Docker rodando**.
- O documento de exemplo "golden" fica em `docs/samples/golden-tcc.json`.

## Documentação

- [docs/adr](docs/adr): decisões de arquitetura, incluindo a separação dos repositórios (ADR 0006).
- [docs/document-model.md](docs/document-model.md): modelo de documento (schema v1).
- [docs/abnt-rules.md](docs/abnt-rules.md): regras ABNT implementadas.
