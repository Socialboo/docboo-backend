# ADR 0003: Sumário e campos (TOC, PAGE)

- **Status:** proposto (implementação na Fase 4)
- **Data:** 2026-11-09

## Contexto
O sumário e a numeração de páginas são campos do Word. Ao importar um `.docx`, o LibreOffice não recalcula os índices automaticamente. Já o Word só atualiza os campos se for instruído a isso.

## Decisão (abordagem híbrida)
1. **Word:** o `settings.xml` recebe `w:updateFields=true`, para o Word oferecer a atualização dos campos ao abrir o arquivo.
2. **Conteúdo pré-preenchido:** o campo TOC já sai com as entradas (títulos e níveis). Assim o sumário nunca aparece vazio, mesmo antes de qualquer atualização.
3. **PDF:** a conversão roda um script Python/UNO no próprio `soffice` headless. O script:
   - abre o `.docx`;
   - chama `update()` em todos os índices (`XDocumentIndexesSupplier`) e `refresh()` nos campos;
   - exporta com o filtro `writer_pdf_Export`.

   Continua sendo um único renderizador.

## Consequências
- O PDF sai com sumário e números de página corretos.
- O Word pode exibir um aviso de "atualizar campos" ao abrir o arquivo, o que é aceitável.
- Os testes de integração verificam se as páginas no sumário do PDF batem com as páginas reais dos títulos.
