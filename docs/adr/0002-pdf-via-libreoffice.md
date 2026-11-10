# ADR 0002: PDF gerado a partir do próprio .docx via LibreOffice

- **Status:** aceito
- **Data:** 2026-11-09

## Contexto
Manter dois renderizadores (um para `.docx` e outro para PDF) leva a divergências de paginação, quebras e estilos.

## Decisão
O `.docx` gerado pelo Exporter (Open XML SDK) é a **única fonte da verdade**. O PDF é obtido convertendo esse arquivo com o LibreOffice headless (`soffice`) dentro do container da API.

- A imagem da API instala `libreoffice-writer-nogui`, `python3-uno`, `fonts-liberation`/`fonts-liberation2` e `fontconfig`. As fontes Liberation têm métricas compatíveis com Times New Roman e Arial.
- Cada job usa um perfil isolado (`-env:UserInstallation=file:///tmp/lo-<jobId>`), o que permite rodar conversões em paralelo com concorrência limitada pela fila.
- A imagem de desenvolvimento (`target: dev`) instala os mesmos pacotes, para o comportamento ser idêntico.

## Consequências
- A imagem fica maior (cerca de 400–500 MB a mais).
- Diferenças entre Word e LibreOffice ainda podem existir. Elas são mitigadas pelo uso de estilos reais, fontes métricas compatíveis e testes de integração que verificam o PDF.
