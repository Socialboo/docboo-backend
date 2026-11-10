# ADR 0005: Persistência do documento, concorrência e usuário local

- **Status:** aceito
- **Data:** 2026-11-09

## Decisão

- **Armazenamento:** a tabela `works` guarda o documento inteiro em uma coluna `jsonb`, via conversor do EF Core. O conversor usa `DocumentJson.Options`, a mesma configuração de serialização da API, então o banco guarda exatamente o JSON que a API devolve.
  - O título é desnormalizado a partir de `metadata.title`, para a listagem não precisar ler o documento.
- **Concorrência otimista:** `version` (int) é o token de concorrência do EF.
  - O cliente envia `version` ao salvar.
  - Se a versão estiver desatualizada, a API responde **409** com `currentVersion`.
  - Salvamentos simultâneos da mesma versão resultam em um único sucesso, garantido pelo `UPDATE ... WHERE version = @old`.
  - Isso é coberto por teste de integração.
- **Saneamento e validação:** acontecem em `Application`, antes de qualquer gravação (ver `docs/document-model.md`). A desserialização já rejeita propriedades desconhecidas e tipos de bloco inválidos.
- **Migrations:**
  - ficam em `Infrastructure/Persistence/Migrations`;
  - uma `IDesignTimeDbContextFactory` permite rodar `dotnet ef` sem subir a API;
  - são aplicadas na inicialização quando `Database:MigrateOnStartup=true` (padrão). Com várias réplicas, isso deve virar um passo de deploy.
- **Usuário:** não há autenticação no MVP.
  - `ICurrentUser` é implementado por `LocalCurrentUser`, que lê o `LocalUser:UserId` configurado.
  - Todas as consultas filtram por `UserId`.
  - Trocar para login real exige uma nova implementação de `ICurrentUser`, sem mudar os casos de uso.
- **IDs:** `Guid.CreateVersion7` (ordenável por tempo).
- **Contratos:** o projeto `DTOs` referencia `Domain`, porque o próprio modelo de documento é parte do contrato HTTP e aparece no OpenAPI com discriminadores obrigatórios.

## Consequências

- Não há consultas relacionais sobre o conteúdo do documento. Para o MVP isso não é necessário, e o `jsonb` ainda permite consultas pontuais.
- O documento inteiro é regravado a cada autosave. Com o limite de 2 MiB, o custo é aceitável. Uma alternativa futura é usar patches (JSON Patch) se o volume crescer.
