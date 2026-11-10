# ADR 0006: Repositórios separados para front e back

- **Status:** aceito (substitui a decisão de monorepo do plano inicial)
- **Data:** 2026-11-10

## Contexto
O front e o back passaram a viver em repositórios próprios, `Socialboo/docboo-frontend` e `Socialboo/docboo-backend`, com ciclos de versão e de deploy independentes.

## Decisão
- **Cada repositório é autocontido:** tem seu próprio `docker-compose.yml`, `docker-compose.dev.yml`, `.env.example`, README e `docs/`.
- **Backend** (este repositório):
  - O compose sobe `db` (PostgreSQL 18), `pgadmin` (pgAdmin 4) e `api`.
  - Postgres e pgAdmin ficam publicados apenas em `127.0.0.1`.
  - A API fica publicada em `5248`.
- **Frontend:**
  - O compose sobe só o `web` (nginx).
  - O nginx faz proxy de `/api` para `API_UPSTREAM`. O padrão é `http://host.docker.internal:5248`, ou seja, a API publicada no host, rodando em Docker ou nativa.
  - Os dois composes não compartilham rede nem dependem um do outro.
- **Contrato:**
  - O OpenAPI continua sendo gerado no build da API (`Backend/openapi/openapi.json`, versionado aqui).
  - O front gera os tipos com `bun run gen:api`. A fonte padrão é a API rodando (`http://localhost:5248/openapi/v1.json`) ou um arquivo/URL em `OPENAPI_SOURCE`.
- **Documentação:** as decisões de backend e do modelo de documento ficam aqui. As decisões de frontend ficam em `docboo-frontend/docs`.

## Consequências
- Uma mudança de contrato vira dois PRs: o da API (com o `openapi.json`) e o do front (com o `schema.gen.d.ts` regenerado).
- Sem dependência entre composes, o front pode rodar contra uma API em qualquer lugar. Em contrapartida, não há `depends_on` entre web e api: subir o backend primeiro é responsabilidade de quem está desenvolvendo.
