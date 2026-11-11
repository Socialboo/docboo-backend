# ADR 0007: Pipeline de exportação (DOCX → PDF), fila e armazenamento

- **Status:** aceito
- **Data:** 2026-11-10

## Contexto
Gerar um PDF leva alguns segundos, porque inicia um LibreOffice. A requisição HTTP não pode ficar esperando, e várias exportações simultâneas não podem derrubar o servidor.

## Decisão

### Fluxo
```
POST /api/works/{id}/exports {format}
  └─ valida a prontidão (metadados exigidos pela capa e pela folha de rosto) → 400 com os caminhos faltantes
  └─ cria um ExportJob com um SNAPSHOT do documento (jsonb) e responde 202 + Location
       ↓ IExportQueue (Channel limitado em memória)
ExportWorker (BackgroundService, Parallel.ForEachAsync com MaxConcurrency)
  └─ ExportJobProcessor: DOCX (Open XML) → [PDF via LibreOffice] → IFileStorage → status succeeded/failed
GET /api/exports/{id}          ← o front faz polling
GET /api/exports/{id}/file     ← 409 enquanto não termina; 410 depois de expirar
```

- **Snapshot:** o job guarda o documento do momento do pedido. Edições feitas depois não alteram uma exportação já pedida (há teste de integração para isso).
- **Fonte única da verdade:** o PDF é sempre a conversão do `.docx` gerado; não existe um segundo renderizador. A conversão roda um script UNO que atualiza o sumário e as listas antes de exportar (ADR 0003).
- **Imagens:** as figuras referenciam imagens enviadas (`assets`), guardadas no mesmo `IFileStorage`. O processador carrega as imagens do *snapshot* antes de gerar o `.docx`.
- **LibreOffice:**
  - perfil isolado por conversão (`-env:UserInstallation` em uma pasta temporária), o que permite rodar em paralelo;
  - timeout configurável, que encerra a árvore de processos;
  - no Windows, usa o `soffice.com`, que espera a conversão terminar.
- **Concorrência:**
  - `Export:MaxConcurrency` (padrão 2) limita quantos jobs rodam ao mesmo tempo;
  - a fila é limitada (`Export:QueueCapacity`); quando enche, o pedido aguarda vaga.
- **Recuperação:**
  - ao iniciar, o worker devolve à fila os jobs que ficaram em `queued` ou `running`;
  - um job interrompido várias vezes (`Export:MaxAttempts`) é marcado como `failed`.
- **Avisos:** conteúdo que o gerador ainda não sabe desenhar não falha a exportação; vira `warnings`, em texto para o aluno.
- **Armazenamento:**
  - `IFileStorage` com implementação em disco (`Storage:Root`; em Docker, o volume `/data/exports`);
  - as chaves são geradas pelo servidor e validadas contra *path traversal*;
  - a gravação é atômica (escreve em `.tmp` e depois renomeia).
- **Expiração:**
  - os arquivos ficam disponíveis por `Export:RetentionHours` (padrão 24 h);
  - o `ExportCleanupService` apaga os arquivos vencidos e marca os jobs como `expired`;
  - apagar um trabalho remove os jobs (em cascata) e os arquivos deles.
- **Healthcheck:** `libreoffice` aparece em `/health` como *Degraded* quando o `soffice` não é encontrado. Nesse caso, a API continua exportando DOCX e recusa pedidos de PDF com 400.

## Extensão
- **Hangfire:** implementar `IExportQueue` enfileirando um job do Hangfire que chame `ExportJobProcessor.ProcessAsync(jobId)` e remover `AddExportWorkers()`. O processador não depende do agendador.
- **MinIO/S3:** implementar `IFileStorage`. O download pode passar a redirecionar para uma URL pré-assinada.

## Consequências
- O front precisa fazer polling (TanStack Query com `refetchInterval` enquanto o status for `queued` ou `running`).
- A fila em memória não é distribuída: com várias réplicas da API, cada uma processa o que recebeu. A recuperação na inicialização cobre reinícios, mas não divide carga entre instâncias, e é por isso que a interface já existe para trocar pelo Hangfire.
