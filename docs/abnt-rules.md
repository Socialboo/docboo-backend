# Regras ABNT implementadas

Este documento lista as regras de **formatação** que o perfil `abnt-tcc` implementa. Ele não reproduz o texto das normas.

Todos os valores ficam no perfil `Exporter/Profiles/abnt-tcc.json`, que é o motor de normas; o gerador não tem medidas fixas no código. Os testes de cada regra estão em `tests/Exporter.Tests`: os de `Docx/` inspecionam o XML do `.docx` e os de `Pdf/` medem o PDF gerado pelo LibreOffice.

## Implementadas (Fase 3)

| Regra | Valor no perfil | Como é aplicada | Verificada em |
| --- | --- | --- | --- |
| Papel | A4 (21 × 29,7 cm) | `w:pgSz` de cada seção | DOCX (twips) e PDF (595 × 842 pt) |
| Margens | sup. 3 cm, esq. 3 cm, inf. 2 cm, dir. 2 cm | `w:pgMar` de cada seção | DOCX e PDF (posição real do texto) |
| Fonte | 12 pt; Times New Roman (padrão) ou Arial (`profileOverrides.fontFamily`) | `docDefaults` + estilo Normal | DOCX e fonte embutida no PDF |
| Corpo | espaçamento 1,5; recuo de primeira linha 1,25 cm; justificado | estilo **Normal** | DOCX |
| Títulos de seção | numeração progressiva (1, 1.1, 1.1.1…) em até 5 níveis, cada nível com aparência distinta | estilos **Heading 1–5** (nomes nativos do Word), lista multinível ligada aos estilos e `outlineLvl` | DOCX e texto do PDF |
| Seção primária | começa em página nova, sempre na mesma altura | `pageBreakBefore` no Heading 1 (sem espaço antes) | PDF |
| Contagem de páginas | conta a partir da folha de rosto; a capa não é contada | seção própria para a capa; `pgNumType start=1` na folha de rosto | DOCX e PDF |
| Número de página | visível a partir da primeira página textual, no canto superior direito, fonte 10 | cabeçalho com campo `PAGE` só na seção textual; a margem do cabeçalho fica a 2 cm do topo | PDF: nenhum número nas pré-textuais; número N−1 na página N, alinhado à margem direita |
| Capa | instituição, autor(es), título: subtítulo, local e ano, distribuídos na página | tabela sem bordas com altura de linhas fixa (mesmo resultado no Word e no LibreOffice) | DOCX e PDF |
| Folha de rosto | autor, título, natureza do trabalho e orientador recuados 8 cm, local e ano | mesma técnica da capa; estilo próprio para a natureza (fonte 10, espaçamento simples) | DOCX e PDF |
| Resumo / abstract | título centralizado em negrito; texto; palavras-chave separadas por ponto e vírgula | estilos próprios (prefixo `Docboo`) | DOCX e PDF |
| Citação longa | recuo de 4 cm, fonte 10, espaçamento simples, sem aspas | estilo **Quote** | DOCX e PDF |
| Alíneas / listas | marcadores ou alíneas `a)`, `b)`… (cada lista recomeça em `a`) | estilo **List Paragraph** + numeração | DOCX |

## Pendentes (Fase 4)

Os itens abaixo ainda não são gerados. Cada exportação devolve um **aviso** listando o que ficou de fora, sem falhar.

| Regra | Valor previsto |
| --- | --- |
| Sumário | campo TOC atualizado na conversão para PDF (ADR 0003) |
| Listas de figuras e de tabelas | campos de índice |
| Citações no texto | sistema autor-data (`(SILVA, 2020, p. 12)` / `Silva (2020, p. 12)`). Hoje sai só o identificador da referência. |
| Notas de rodapé | fonte 10, espaçamento simples |
| Figuras e tabelas | legenda e fonte em fonte 10, espaçamento simples |
| Referências | alinhadas à esquerda, espaçamento simples, linha em branco entre itens, ordem alfabética |
| Apêndices e anexos | títulos "APÊNDICE A — …" e "ANEXO A — …" |
