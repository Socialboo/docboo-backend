# Modelo de documento — schema v1

O documento é a **fonte da verdade** de um trabalho. Ele é um JSON versionado (`schemaVersion`):
- editado pelo front;
- salvo pela API em uma coluna `jsonb`;
- consumido pelo Exporter.

As classes C# ficam em `Domain/Documents`. No front (repositório `docboo-frontend`), os tipos são **gerados** a partir do OpenAPI (`components["schemas"]["DocumentModel"]`).

Exemplo completo: [`samples/golden-tcc.json`](samples/golden-tcc.json). Esse arquivo também é usado pelos testes.

## Estrutura

```
DocumentModel
├─ schemaVersion: 1
├─ profileId: "abnt-tcc"              (igual ao perfil do trabalho; não muda após a criação)
├─ profileOverrides { fontFamily: "timesNewRoman" | "arial" | null }
├─ metadata { institution, faculty?, course, authors[], title, subtitle?, advisor, coAdvisor?, city, year?, nature }
├─ preTextual
│  ├─ cover { enabled }, titlePage { enabled }
│  ├─ abstractPt / abstractEn { enabled, text, keywords[] }
│  └─ lists { figures, tables }        (o sumário é sempre gerado)
├─ textual.sections[]                  Section { id, title, blocks[], children[] }  (nível = profundidade, 1..5)
├─ postTextual { appendices[], annexes[] }   Attachment { id, title, blocks[] }
└─ references[]                        CSL-JSON (campos comuns tipados; demais em extensão)
```

### Blocos (`type`)

| type | Campos |
| --- | --- |
| `paragraph` | `content: Inline[]` |
| `longQuote` | `content: Inline[]`. A formatação (recuo de 4 cm, fonte 10) vem do perfil. |
| `list` | `ordered`, `items: [{ content: Inline[] }]` |
| `figure` | `assetId`, `caption: Inline[]`, `source: Inline[]`, `widthPercent` (10–100) |
| `table` | `title: Inline[]`, `source: Inline[]`, `rows: [{ header, cells: [{ content, colSpan, rowSpan }] }]` |

### Inlines (`type`)

O formato espelha o editor Tiptap/ProseMirror.

| type | Campos |
| --- | --- |
| `text` | `text`, `marks: ("bold" \| "italic" \| "underline" \| "superscript" \| "subscript")[]` |
| `hardBreak` | (sem campos) |
| `citation` | `refIds: string[]`, `locator?: string`, `mode: "parenthetical" \| "narrative"` |
| `footnote` | `id`, `content: Inline[]`. Uma nota não pode conter outra nota. |

## Regras de serialização

- Os nomes de propriedade usam camelCase. Os enums são strings em camelCase.
- Campos de CSL-JSON mantêm os nomes originais (`container-title`, `URL`, …).
- **Todas as propriedades são obrigatórias**: os campos opcionais são enviados como `null`.
- **Propriedades desconhecidas são rejeitadas** com HTTP 400. A exceção são as referências CSL, que guardam campos extras.
- O discriminador `type` pode aparecer em qualquer posição dentro do objeto.

## Saneamento (antes de validar e salvar)

O modelo não aceita HTML. A estrutura só entra pelos tipos acima. Sobre o texto, o saneamento faz o seguinte:
- remove caracteres de controle e surrogates inválidos;
- aplica Unicode NFC;
- converte tabulações em espaço;
- converte quebras de linha em espaço nos campos de uma linha (o resumo mantém `\n`);
- apara as bordas dos campos de uma linha;
- descarta trechos de texto vazios e marcas duplicadas.

## Validação (salvamento de rascunho)

Na validação estrutural, metadados vazios são aceitos. A completude para exportação é verificada na exportação. Os erros voltam como `ValidationProblemDetails`, com chaves no formato de caminho JSON, por exemplo `textual.sections[0].blocks[2].content[1].refIds[0]`.

- `schemaVersion` suportado; `profileId` existente e igual ao do trabalho.
- Seções com até `maxSectionDepth` níveis (5 no perfil ABNT).
- Ids no formato `^[A-Za-z0-9][A-Za-z0-9_-]*$`, com até 64 caracteres. Os ids de seções, blocos, anexos e notas são únicos no documento; os ids de referências são únicos entre si.
- Citações apontam para referências existentes; notas de rodapé não são aninhadas.
- Figuras: largura entre 10% e 100%. Tabelas: `colSpan` e `rowSpan` coerentes, com até 30 colunas e 1.000 linhas.
- URL de referência: apenas http(s) absoluta.
- Limites configuráveis (`DocumentLimits` em `appsettings.json`):
  - documento serializado com até 2 MiB;
  - trecho de texto com até 20.000 caracteres;
  - campos curtos com até 500 caracteres;
  - até 20.000 blocos e 2.000 referências.
- Limite do corpo da requisição de salvamento: 4 MiB. Acima disso, a resposta é HTTP 413.

## Evolução do schema

Uma mudança incompatível exige incrementar `schemaVersion` e acrescentar uma migração do JSON (v1 → v2) no carregamento. A exportação sempre trabalha com a versão atual.
