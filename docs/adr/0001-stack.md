# ADR 0001: Stack e estrutura da solução

- **Status:** aceito
- **Data:** 2026-11-09

## Contexto
Precisamos de um editor web estruturado que exporte `.docx` e `.pdf` fiéis a normas acadêmicas, começando pela ABNT.

## Decisão
- **Frontend** (repositório `docboo-frontend`): React, TypeScript e Vite, com TanStack Router, Query e Form. Componentes shadcn no estilo `base-nova` (Base UI) e editor Tiptap. Gerenciador de pacotes: bun.
- **Backend** (este repositório, `docboo-backend`): ASP.NET Core (.NET 10 LTS) com **Controllers**. A solução se divide em:
  - `Backend`: host HTTP, Controllers, OpenAPI e DI.
  - `DTOs`: contratos HTTP expostos no OpenAPI.
  - `Domain`: entidades e modelo do documento.
  - `Application`: casos de uso, validação e portas (`IFileStorage`, `IExportQueue`, `IPdfConverter`, …).
  - `Infrastructure`: EF Core/PostgreSQL, storage em disco e fila com `Channels` + `BackgroundService`.
  - `Exporter`: perfis de norma, geração com Open XML SDK e conversão com o LibreOffice.
- **Dependências entre projetos:**
  - Backend → Application, Infrastructure, DTOs
  - Application → Domain, DTOs
  - Infrastructure → Application, Exporter
  - Exporter → Domain
  - DTOs → Domain (o modelo de documento faz parte do contrato; ver ADR 0005)
- **Contrato:** o OpenAPI (`Microsoft.AspNetCore.OpenApi`) é gerado no build. O front gera seus tipos com `openapi-typescript` e consome a API com `openapi-fetch`. A documentação fica em Swagger UI e Scalar.
- **Repositórios:** a ideia inicial era um monorepo. O front e o back foram separados em repositórios próprios (ver ADR 0006).

## Consequências
- Os tipos do front nunca são escritos à mão. Um contrato alterado exige rodar `dotnet build` e `bun run gen:api`.
- As camadas permitem trocar a fila (Hangfire) e o storage (MinIO/S3) sem tocar nos casos de uso.
