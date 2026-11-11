# docboo-backend

API do **Docboo**, o formatador de trabalhos acadêmicos (MVP: ABNT para TCC/monografia). O aluno monta o trabalho no editor do [docboo-frontend](https://github.com/Socialboo/docboo-frontend), e a API guarda o documento e gera os arquivos `.docx` e `.pdf` formatados.

**Stack:**
- ASP.NET Core (.NET 10) com Controllers;
- EF Core + PostgreSQL;
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
| `127.0.0.1:5432` | PostgreSQL |

### Imagens `latest`

Todas as imagens usam a tag `latest` (`postgres`, `dpage/pgadmin4`, `mcr.microsoft.com/dotnet/sdk` e `aspnet`). Para atualizar, rode `docker compose pull`.

> **Atenção:** quando `postgres:latest` mudar de versão principal (por exemplo, 18 → 19), o servidor sobe com um banco **vazio** em um novo diretório do volume. Antes de atualizar, faça `pg_dump` dos dados e restaure depois.

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

Pré-requisitos: .NET SDK 10, Docker (só para o Postgres) e, para exportar em PDF, o [LibreOffice](https://www.libreoffice.org/download/) instalado.

```bash
docker compose up -d db pgadmin
```

```bash
dotnet run --project Backend --launch-profile http
```

A API sobe em http://localhost:5248 e aplica as migrations na inicialização.

- O `soffice` é localizado automaticamente:
  - no Windows, em `C:\Program Files\LibreOffice\program\soffice.com`;
  - no Linux, pelo `PATH`;
  - no macOS, no `/Applications`.
- Para usar outro caminho, defina `LibreOffice__SofficePath`.
- Sem LibreOffice, `/health` mostra `libreoffice: Degraded`, e só a exportação em `.docx` fica disponível.
- Os arquivos exportados ficam em `Backend/data/exports`, que é ignorado pelo git.

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
| DELETE | `/api/works/{id}` | Remove o trabalho, junto com as exportações, as imagens e os arquivos |
| POST | `/api/works/{id}/assets` | Envia uma imagem (`multipart/form-data`, campo `file`). Aceita só PNG ou JPEG, detectados pelo conteúdo e não pelo nome. O `id` devolvido é o `assetId` da figura |
| GET | `/api/works/{id}/assets` | Imagens do trabalho |
| GET | `/api/assets/{id}` | Conteúdo da imagem |
| POST | `/api/works/{id}/exports` | Pede uma exportação `{ format: "docx" \| "pdf" }`. Responde 202 com o job, ou 400 com os dados que faltam |
| GET | `/api/works/{id}/exports` | Últimas exportações do trabalho |
| GET | `/api/exports/{id}` | Status do job (`queued`, `running`, `succeeded`, `failed`, `expired`) e avisos |
| GET | `/api/exports/{id}/file` | Download. Responde 409 enquanto o job não termina e 410 depois que o arquivo expira |

O modelo de documento está descrito em [docs/document-model.md](docs/document-model.md).

### Exportação

A exportação roda em segundo plano:
1. O `.docx` é gerado com Open XML a partir de um *snapshot* do documento.
2. Para PDF, esse mesmo `.docx` é convertido pelo LibreOffice headless, por um script UNO que **atualiza o sumário e as listas** antes de exportar (ADR 0003).
3. O front acompanha o status por polling.

Os detalhes estão no [ADR 0007](docs/adr/0007-export-pipeline.md), e as regras de formatação aplicadas em [docs/abnt-rules.md](docs/abnt-rules.md).

| Configuração | Padrão | O quê |
| --- | --- | --- |
| `Export:MaxConcurrency` | 2 | Exportações processadas ao mesmo tempo |
| `Export:RetentionHours` | 24 | Tempo em que o arquivo fica disponível |
| `Export:CleanupIntervalMinutes` | 30 | Intervalo da limpeza dos arquivos vencidos |
| `Storage:Root` | `data/exports` | Pasta dos arquivos (em Docker, o volume `/data/exports`) |
| `LibreOffice:SofficePath` | (auto) | Caminho do `soffice` |
| `LibreOffice:PythonPath` | (auto) | Python com o módulo `uno`: no Windows e no macOS, o que vem com o LibreOffice; no Linux, o `python3` |
| `Assets:MaxBytes` | 10 MiB | Tamanho máximo de cada imagem |
| `Assets:MaxDimensionPixels` | 10000 | Largura e altura máximas de cada imagem |
| `LibreOffice:TimeoutSeconds` | 120 | Tempo máximo de cada conversão |

## Banco e migrations

```bash
dotnet ef migrations add NomeDaMigration --project Infrastructure --startup-project Infrastructure --output-dir Persistence/Migrations
```

Para desativar a migração automática na inicialização, use `Database__MigrateOnStartup=false`.

## Testes

```bash
dotnet test docboo-backend.slnx
```

- `Exporter.Tests`: motor de normas, XML do `.docx` (incluindo validação de schema OpenXML) e o PDF real do documento "golden" (páginas, numeração, margens, fonte). Os testes de PDF são pulados se o LibreOffice não estiver instalado.
- `IntegrationTests`: exportação de ponta a ponta pela API (fila, worker, armazenamento, expiração). Também sobe um PostgreSQL descartável (`postgres:latest`) com Testcontainers, por isso **precisa do Docker rodando**. O container é removido ao final; a imagem `testcontainers/ryuk`, que o Testcontainers usa para limpeza, é baixada de novo a cada execução.
- O documento de exemplo "golden" fica em `docs/samples/golden-tcc.json`.

## Documentação

- [docs/adr](docs/adr): decisões de arquitetura, incluindo a separação dos repositórios (ADR 0006).
- [docs/document-model.md](docs/document-model.md): modelo de documento (schema v1).
- [docs/abnt-rules.md](docs/abnt-rules.md): regras ABNT implementadas e pendentes.
- `tests/Exporter.Tests/bin/.../out/golden-tcc.{docx,pdf}`: `.docx` e PDF do documento "golden", gerados pelos testes para conferência visual.
