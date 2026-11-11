# Regras ABNT implementadas

Este documento lista as regras de **formatação** que o perfil `abnt-tcc` implementa. Ele não reproduz o texto das normas.

Todos os valores ficam no perfil `Exporter/Profiles/abnt-tcc.json`, que é o motor de normas; o gerador não tem medidas fixas no código. Os testes de cada regra estão em `tests/Exporter.Tests`:
- `Docx/` inspeciona o XML do `.docx`, incluindo a validação de schema OpenXML;
- `Pdf/` mede o PDF real gerado pelo LibreOffice: posições, fontes e números de página;
- `References/` cobre a formatação de citações e referências.

## Página e texto

| Regra | Valor no perfil | Como é aplicada | Verificada em |
| --- | --- | --- | --- |
| Papel | A4 (21 × 29,7 cm) | `w:pgSz` de cada seção | DOCX e PDF |
| Margens | sup. 3 cm, esq. 3 cm, inf. 2 cm, dir. 2 cm | `w:pgMar` de cada seção | DOCX e PDF (posição real do texto) |
| Fonte | 12 pt; Times New Roman (padrão) ou Arial (`profileOverrides.fontFamily`) | `docDefaults` + estilo Normal | DOCX e fonte embutida no PDF |
| Corpo | espaçamento 1,5; recuo de primeira linha 1,25 cm; justificado | estilo **Normal**; quebra de linha manual não estica a linha (`doNotExpandShiftReturn`) | DOCX e PDF |
| Citação longa | recuo de 4 cm, fonte 10, espaçamento simples, sem aspas | estilo **Quote** | DOCX e PDF |
| Alíneas e listas | marcadores, ou alíneas `a)`, `b)`… (cada lista recomeça em `a`) | estilo **List Paragraph** + numeração | DOCX |

## Títulos, sumário e paginação

| Regra | Valor no perfil | Como é aplicada | Verificada em |
| --- | --- | --- | --- |
| Títulos de seção | numeração progressiva (1, 1.1, 1.1.1…) em até 5 níveis, cada nível com aparência distinta | estilos **Heading 1–5** + lista multinível ligada aos estilos | DOCX e PDF |
| Seção primária | começa em página nova, sempre na mesma altura | `pageBreakBefore` no Heading 1, sem espaço antes | PDF |
| Sumário | todos os títulos com indicativo e página; a formatação de cada nível espelha o título; elementos pós-textuais sem indicativo | campo `TOC \o "1-5" \u`, atualizado na conversão para PDF (ADR 0003) | PDF: **cada entrada é conferida contra a página real do título** |
| Contagem de páginas | conta a partir da folha de rosto; a capa não é contada | seção própria para a capa; `pgNumType start=1` na folha de rosto | DOCX e PDF |
| Número de página | visível a partir da primeira página textual (inclusive nos pós-textuais), canto superior direito, fonte 10 | cabeçalho com campo `PAGE` só na seção textual | PDF: nenhum número nas pré-textuais; a página N mostra N−1 |

## Elementos pré-textuais

| Regra | Valor no perfil | Como é aplicada | Verificada em |
| --- | --- | --- | --- |
| Capa | instituição, autor(es), título: subtítulo, local e ano | tabela sem bordas com altura de linhas fixa (mesmo resultado no Word e no LibreOffice) | DOCX e PDF |
| Folha de rosto | autor, título, natureza do trabalho e orientador (recuo de 8 cm, fonte 10, espaçamento simples), local e ano | mesma técnica da capa | DOCX e PDF |
| Resumo / abstract | título centralizado em negrito; palavras-chave separadas por ponto e vírgula | estilos próprios (prefixo `Docboo`) | DOCX e PDF |
| Listas de figuras e de tabelas | "Figura 1 – Título ....... página" | campo `TOC \c "Figura"` / `\c "Tabela"`; só aparece se houver itens e a opção estiver ativa | PDF: a página da entrada é conferida |

## Ilustrações, tabelas e notas

| Regra | Valor no perfil | Como é aplicada | Verificada em |
| --- | --- | --- | --- |
| Figuras | identificação acima ("Figura 1 – Título"); fonte abaixo ("Fonte: …"); fonte 10, espaçamento simples | estilo **caption** com campo `SEQ Figura`; imagem embutida com a largura relativa (`widthPercent`) e a proporção preservada | DOCX e PDF (imagem entre a legenda e a fonte) |
| Tabelas | título acima e fonte abaixo; laterais abertas; traços no topo, abaixo do cabeçalho e na base; texto em fonte 10 | campo `SEQ Tabela`; bordas da tabela e da célula; o cabeçalho se repete em cada página; células mescladas (`gridSpan` / `vMerge`) | DOCX |
| Notas de rodapé | fonte 10, espaçamento simples, separadas por filete | `footnotes.xml` com os estilos **footnote text** e **footnote reference** | DOCX e PDF (no rodapé da página, em 10 pt) |

## Citações e referências

| Regra | Comportamento | Verificada em |
| --- | --- | --- |
| Citação autor-data | parentética `(SOUZA; PEREIRA, 2021, p. 12)` e narrativa `Souza e Pereira (2021, p. 12)` | unitário + DOCX + PDF |
| Muitos autores | até 3 são listados; com 4 ou mais, o primeiro seguido de `et al.` | unitário |
| Autor institucional e obra sem autor | nome da entidade em caixa alta; sem autor, a primeira palavra do título seguida de `...` | unitário |
| Mesmo autor e ano | sufixos `2019a`, `2019b` na citação e na referência | unitário |
| Lista de referências | ordem alfabética (ignorando acentos); alinhada à esquerda; espaçamento simples; linha em branco entre itens | unitário + DOCX + PDF |
| Formato das entradas | sobrenome em caixa alta; título (ou periódico) em **negrito**, sem o subtítulo; dados ausentes como `[S. l.]`, `[s. n.]` e `[s. d.]`; `Disponível em:` / `Acesso em:`; DOI | unitário |
| Tipos de referência | livro, capítulo (`In:`), artigo de periódico, trabalho acadêmico (tese/dissertação/TCC), página web, evento e relatório | unitário |

## Pós-textuais

| Regra | Como é aplicada |
| --- | --- |
| Ordem | referências, apêndices e anexos |
| Títulos | centralizados, sem indicativo numérico, em página nova; aparecem no sumário |
| Apêndices e anexos | "APÊNDICE A – Título" e "ANEXO A – Título" (A…Z, depois AA, AB…) |
