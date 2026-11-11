# ADR 0003: Sumário, listas e campos (TOC, SEQ, PAGE)

- **Status:** aceito e implementado (Fase 4)
- **Data:** 2026-11-09 (proposto) / 2026-11-11 (implementado)

## Contexto
O sumário, as listas de figuras e tabelas e a numeração de páginas são **campos** do Word, e os números de página só existem depois da paginação.
- O LibreOffice **não** recalcula índices ao importar um `.docx`: um `soffice --convert-to pdf` simples imprime as entradas como estavam no arquivo.
- O Word só atualiza os campos se for instruído a isso.

## Decisão
1. **Campos de verdade no `.docx`:**
   - Sumário: `TOC \o "1-5" \h \z \u`. O `\u` inclui os títulos pós-textuais, que têm nível de estrutura 1 mas não são numerados.
   - Listas: `TOC \h \z \c "Figura"` e `\c "Tabela"`.
   - Legendas: `SEQ Figura` e `SEQ Tabela`, com os rótulos vindos do perfil.
   - Número de página: `PAGE` no cabeçalho.
2. **Entradas pré-preenchidas:** o gerador já escreve as entradas do sumário e das listas, com o texto e a numeração de seção corretos. Assim o índice nunca aparece vazio. Só os números de página ficam para a atualização.
3. **Word:** o `settings.xml` recebe `w:updateFields=true`. Ao abrir o arquivo, o Word pergunta se deve atualizar os campos e preenche as páginas.
4. **PDF:** a conversão roda o `Exporter/Pdf/convert_to_pdf.py` (UNO) contra um `soffice` headless que escuta em um *pipe* exclusivo do job. O script:
   - abre o `.docx` oculto;
   - chama `update()` em todos os índices e `refresh()` nos campos, em **duas passadas** (a primeira pode empurrar páginas e a segunda acerta os números);
   - exporta com `writer_pdf_Export`.

   Continua havendo um único renderizador.
5. **Python do UNO:**
   - no Windows e no macOS, o que vem com o LibreOffice (`program/python.exe`);
   - no Linux, o `python3` do sistema com o pacote `python3-uno`, que já está instalado na imagem Docker;
   - configurável em `LibreOffice:PythonPath`.

## Verificação
Os testes de PDF (`tests/Exporter.Tests/Pdf`) leem o sumário gerado e conferem, **para cada entrada**, que o título aparece na página indicada. Fazem o mesmo para as listas de figuras e de tabelas.

## Consequências
- A conversão para PDF passou de cerca de 3 s para cerca de 5 a 6 s (sobe o LibreOffice e conecta via UNO). Isso é aceitável, porque roda em segundo plano.
- No Word, o aluno vê o aviso de atualização de campos ao abrir o arquivo.
- Abrindo o `.docx` direto no LibreOffice, as páginas do sumário ficam vazias até o usuário atualizar o índice manualmente (Ferramentas → Atualizar).
