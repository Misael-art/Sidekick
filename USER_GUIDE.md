# Sidekick - Guia do Usuario

> Automacao visual para Windows. Monte flows conectando blocos, sem escrever codigo.

---

## 1. Primeiros passos

### 1.1 Requisitos
- Windows 10/11 (64-bit)
- .NET 8 Runtime na versao framework-dependent, ou nada extra na versao standalone
- WebView2 Runtime, quando necessario

### 1.2 Instalacao
1. Extraia `Sidekick.zip` em qualquer pasta.
2. Execute `Sidekick.exe`.
3. O icone do Sidekick aparecera na bandeja do sistema.

### 1.3 Estrutura da interface
- **Toolbar:** novo flow, salvar, carregar, Marketplace, Export Runner, Run Now, Arm/Disarm, Stop, Mira, Mira Lib, Snip, Sticky, nome do flow e seletor de idioma `PT-BR`/`English`.
- **Palette:** coluna retratil com nodes agrupados por categoria.
- **Canvas:** area onde o flow e montado. Clique com o botao direito para abrir o menu rapido, ou arraste um fio de saida e solte em area vazia para escolher o proximo node ja conectado.
- **Property Panel:** configuracoes do node selecionado. Campos de escolha fechada, como `Button`, `Click Type`, `Format`, `Mode` e `Operation`, aparecem como listas para evitar erro de digitacao.
- **Status Bar:** estado do runtime, fila, flows armados e logs.

---

## 2. Conceitos basicos

### 2.1 O que e um flow
Um flow e uma automacao completa formada por nodes conectados por fios.

### 2.2 Categorias de nodes
O palette organiza nodes por area de uso, nao apenas por tipo tecnico:

| Categoria | Funcao |
|-----------|--------|
| Trigger | Inicia o flow quando um evento ocorre |
| Desktop | Clica, le, espera ou manipula elementos e entrada do desktop |
| Window | Controla janelas e processos |
| Hardware | Audio, microfone, camera, Wi-Fi, energia e display |
| Media | Screenshot, gravacao, imagem, som e overlays |
| Console | Comandos, PowerShell/CMD, PWD e variaveis de ambiente |
| Logic | Condicoes, loops, variaveis, delays e composicao |
| Data | Arquivos, clipboard, HTTP, CSV/JSON e dados |
| Utility | Nodes auxiliares que nao se encaixam nas categorias acima |

### 2.3 Criacao visual no canvas
Voce nao precisa arrastar tudo da sidebar.

- Clique com botao direito no canvas para abrir o menu de busca e adicionar um node.
- Arraste um fio de uma porta de saida e solte no vazio para abrir **Adicionar passo**; ao escolher um node compativel, o Sidekick conecta automaticamente.
- Clique com botao direito em um fio para inserir um novo node no meio ou remover a conexao.
- Clique com botao direito em um node para duplicar, desabilitar/habilitar ou remover.
- Clique em **Sticky** para criar notas no canvas. A nota pode ter titulo, corpo, cor, tamanho e posicao; tambem pode ser duplicada/removida pelo menu de contexto. Stickies sao salvas no flow, mas nao executam no runtime.
- Node desabilitado nao e apagado. Ao executar/validar, o Sidekick tenta fazer bypass quando o node tem uma entrada e uma saida de fluxo.
- Use `Ctrl+D` para duplicar, `Ctrl+0` para ajustar a visao, `Ctrl+K` ou `/` para busca rapida, `C` para conectar um proximo passo a partir do node selecionado e `L` para auto layout.
- Se uma conexao falhar, leia a mensagem: portas `Flow` so conectam com `Flow`; saidas de dados devem entrar em portas de dados compativeis.

### 2.4 Ports
- Triggers nao possuem entrada.
- A maioria dos nodes usa `In` e `Out`.
- Nodes de decisao podem expor saidas como `True/False` ou `Match/NoMatch`.

### 2.5 Variaveis
Use `{{nomeDaVariavel}}` em campos de texto para interpolar valores do contexto.

### 2.6 Run Now vs Arm
O runtime do Sidekick separa claramente os conceitos:

- **Run Now:** executa o flow uma vez usando o snapshot atual do editor.
- **Arm:** deixa os triggers do flow ativos em background.
- **Disarm:** interrompe o monitoramento do flow.
- **Stop:** interrompe a execucao atual. Se existir fila pendente, voce pode parar apenas a execucao atual ou limpar toda a fila.

Regra pratica:

- Use **Run Now** para testes e execucoes pontuais.
- Use **Arm** para automacoes continuas baseadas em hotkey, arquivos, janelas, imagens ou pixels.

---

## 3. Seu primeiro flow

### Exemplo 1: hotkey que digita um texto
1. Arraste `Hotkey Trigger` para o canvas.
2. Configure a tecla, por exemplo `F9`, e o modificador `Ctrl`.
3. Arraste `Keyboard Type`.
4. Defina o texto, por exemplo `Ola, mundo!`.
5. Conecte `Triggered` -> `In`.
6. Clique em **Arm**.
7. Pressione `Ctrl+F9` em qualquer lugar para disparar a automacao.

Dica: para criar como n8n/Blender, arraste o ponto de saida de um node e solte em uma area vazia do canvas. O Sidekick abre a biblioteca de nodes exatamente naquele ponto e conecta automaticamente quando a porta for compativel.

### Exemplo 2: monitor de pasta com som
1. Arraste `File System Trigger` e configure a pasta, filtro e tipo de evento.
2. Arraste `Play Sound`.
3. Conecte `Triggered` -> `In`.
4. Clique em **Arm**.

### Exemplo 3: teste manual de um flow armado
1. Com o flow ainda aberto no editor, clique em **Run Now**.
2. O flow sera enfileirado para uma execucao imediata.
3. O fato de o flow estar armado ou nao nao altera o JSON salvo; o arm/disarm e estado de runtime.

### Exemplo 4: esperar um texto e clicar em um botao
1. Abra o app alvo.
2. Clique em **Mira** e capture o texto ou botao que quer automatizar.
3. Adicione `Desktop Wait Element` para esperar o texto aparecer.
4. Adicione `Desktop Click Element` para clicar no botao.
5. No **Property Panel**, use **Use Latest** ou **Browse Mira** para aplicar a captura ao node.
6. Clique em **Test Selector** na biblioteca do Mira quando quiser conferir se o alvo ainda e encontrado.
7. Use **Run Now** para testar e **Arm** quando o flow tiver um trigger continuo.

---

## 4. Referencia rapida de nodes

### 4.1 Triggers
- **Hotkey Trigger:** dispara com atalho global.
- **File System Trigger:** observa criacao, alteracao ou remocao de arquivos.
- **Window Event Trigger:** observa abertura, fechamento e foco de janelas.
- **Pixel Change Trigger:** monitora alteracao de cor em um ponto.
- **Image Detected Trigger:** procura uma imagem na tela.
- **Desktop Element Appeared:** dispara quando um elemento desktop aparece.
- **Desktop Text Changed:** dispara quando o texto de um elemento desktop muda.
- **Schedule Time:** dispara em um horario local fixo.
- **Interval:** dispara em intervalos recorrentes.
- **Process Event:** observa inicio/parada de processo.

### 4.2 Logic
- **If / Else:** desvia o fluxo com base em uma condicao.
- **Delay:** pausa a execucao.
- **Loop:** repete um bloco N vezes.
- **Set Variable / Get Variable:** grava e le variaveis.
- **Compare Text:** compara strings.
- **Cooldown:** evita repeticoes muito proximas na mesma execucao.
- **Condition Group:** combina condicoes `ANY/ALL` com operadores `equals/contains/regex/greater/less/exists/changed`.
- **Until Date/Time:** desvia antes/depois de um horario local, como `00:00`.
- **Daily Reset:** ajuda a liberar bloqueios quando muda o dia.

### 4.3 Actions
- **Mouse Click / Move / Drag**
- **Keyboard Type / Press**
- **Open Program**
- **Kill Process**
- **Wait Process**
- **Window Control**
- **Desktop Wait Element**
- **Desktop Click Element**
- **Desktop Read Element Text**
- **Click Image Match**
- **Capture Screenshot**
- **Record Desktop**
- **Record Camera**
- **Overlay Solid Color**
- **Overlay Image**
- **Overlay Text**
- **Console Set Directory**
- **Console Command**
- **System Audio**
- **Hardware Device**
- **System Power**
- **Display Settings**
- **Require Admin / Restart as Admin**
- **Windows Theme / Accent / Wallpaper / Explorer / Taskbar**
- **Install App / Download File / Verify Checksum**
- **Persist State / Read State**
- **Play Sound**
- **Delete File**

---

## 5. Ferramentas especiais

### 5.1 Mira
Use o modo **Mira** para inspecionar elementos na tela e capturar metadados de interface.

Fluxo pratico atual:

1. Clique em **Mira** na toolbar e capture um elemento da interface.
2. Selecione um node desktop no canvas, como `Desktop Wait Element`, `Desktop Click Element` ou `Desktop Read Element Text`.
3. No **Property Panel**, use o bloco `Mira Selector`.
4. Escolha **Use Latest** para aplicar a captura mais recente, ou **Browse Mira** para selecionar um ativo salvo.
5. O Sidekick preenche `windowTitle`, `windowTitleMatch`, `processName`, `processPath`, `automationId`, `elementName` e `controlType` quando o node suporta esses campos.

O overlay do Mira tambem mostra:

- Texto detectado
- Texto atual
- Placeholder/Hint
- Origem do texto (`UIAutomation`, `ValuePattern`, `TextPattern`, `Legacy`, `OCR` ou fallback)
- Qualidade da captura (`forte`, `media`, `fraca`)

Quando OCR local nao estiver disponivel, o Sidekick avisa que a captura esta usando fallback visual/seletor em vez de prometer leitura OCR.

O Mira mostra dados pensados para automacao:

- nome do elemento, janela, processo e caminho do executavel
- class name, automation id, control type e bounds absolutos/relativos
- cursor, cor sob o cursor, foco, enabled e visible
- estrategia sugerida e robustez do seletor (`forte`, `media` ou `fraca`)
- preview do ativo salvo e acao **Test Selector**

Dicas:

- Use **Browse Mira** quando quiser reutilizar um seletor salvo em varios flows.
- Use **Use Latest** quando estiver ajustando um flow rapidamente logo apos a inspecao.
- Se necessario, clique em **Clear** para limpar o binding e editar os campos manualmente.
- Na **Mira Lib**, cada captura salva aparece com thumbnail, nome, notas e tags. Voce pode buscar por nome, texto detectado, processo, janela, robustez ou tag; tambem pode duplicar, apagar, editar metadata e rodar **Test Selector**.

### 5.2 Snip
Use **Snip** para capturar uma regiao da tela e reutilizar a imagem como template.

Fluxo pratico atual:

1. Capture a regiao com **Snip**.
2. Selecione um `Image Detected Trigger` ou `Click Image Match` no canvas.
3. No **Property Panel**, abra o seletor do campo de template.
4. Escolha o ativo salvo do `Snip` ou use a opcao de aplicar o mais recente.
5. Use `Image Detected Trigger` para monitorar em background ou `Click Image Match` para clicar uma imagem como fallback visual.

Limite atual: OCR ainda nao e recurso de produto. Quando UIAutomation nao encontra um app moderno/hibrido, use `Snip` + `Click Image Match` como fallback visual honesto.

### 5.3 Overlays visuais
Use overlays quando o flow precisa sinalizar algo na tela, bloquear visualmente uma area, apresentar uma mensagem, ou projetar uma imagem/texto por tempo controlado.

- **Overlay Solid Color:** cobre a tela inteira ou uma regiao com cor solida.
- **Overlay Image:** exibe imagens com `contain`, `cover`, `stretch` ou tamanho original.
- **Overlay Text:** exibe texto com fonte, cor, tamanho, efeito, alinhamento e background.

Propriedades comuns:

- `Timer (ms)`: tempo de exibicao.
- `Wait For Timer`: se ligado, o flow espera o overlay terminar antes de seguir.
- `Plane`: `foreground` fica acima das janelas normais; `normal` nao força topo.
- `Full Screen`: cobre todos os monitores; desligue para usar `x/y/width/height`.
- `Opacity`, `Click Through`, `Motion`, `Fade In` e `Fade Out`.

### 5.4 Captura e gravacao
Use **Capture Screenshot** para capturar:

- tela completa (`fullDesktop`)
- monitor especifico
- regiao (`x/y/width/height`)
- janela ativa
- janela por seletor (`windowTitle/processName/processPath`)

Saidas principais: `filePath`, `width`, `height`, `target` e `error`.

Use **Record Desktop** para gravar video da tela. O node grava frames com `ScreenCapture` e codifica com `VideoWriter` (Emgu CV). A gravacao respeita cancelamento do runtime.

Use **Record Camera** para gravar webcam com `VideoCapture` + `VideoWriter` (Emgu CV), com opcao de mirror, crop, efeito e timestamp.

Limitacao atual (honesta): gravacao de audio ainda nao esta implementada.

### 5.5 Console e PWD
Use **Console Set Directory** para definir a variavel `pwd` do flow.

Use **Console Command** para executar comandos com:

- modo `direct`, `cmd` ou `powershell`
- `workingDirectory` com suporte a `{{pwd}}`
- `timeoutMs`
- captura de `stdout`, `stderr` e `exitCode`
- porta `error` quando o comando falha, se `Fail On Non-zero Exit` estiver ligado

Importante: comandos de console podem alterar arquivos, processos e sistema. Revise sempre o comando antes de executar ou armar um flow.

### 5.6 Hardware e sistema
Use **System Audio** para aumentar, abaixar ou definir volume, mutar/desmutar saida de audio e mutar/desmutar microfone.

Use **Hardware Device** para listar ou ligar/desligar dispositivos de camera, microfone e Wi-Fi. Alteracoes reais exigem `Allow System Changes=true` e podem precisar de permissao de administrador do Windows.

Use **System Power** para bloquear a sessao, suspender, hibernar, reiniciar, desligar, fazer logoff ou cancelar shutdown. Operacoes destrutivas exigem a frase `CONFIRM` em **Safety Phrase**.

Use **Display Settings** para descrever monitores e, quando liberado, alterar resolucao, taxa, rotacao e posicao em layout multi-monitor. Alteracoes reais exigem `Allow System Changes=true`.

Regra pratica: teste primeiro com operacoes de leitura como `getState`, `listDevices` e `describe`. So libere mudancas de sistema depois de revisar o flow inteiro.

### 5.7 Windows automation pack
Use os nodes de Windows quando precisar controlar o ambiente do usuario:

- **Taskbar:** mostrar/ocultar, alinhar, abrir app por caminho e registrar limites honestos de pin/unpin.
- **Theme/Wallpaper:** trocar modo claro/escuro, cor de destaque, wallpaper por imagem/cor e restaurar caminho anterior.
- **Desktop/Explorer:** refresh, abrir pasta, selecionar arquivo, criar atalho e reiniciar Explorer.
- **Restore Point:** criar/listar/abrir restauracao com dry-run e aviso de admin.
- **Admin:** `Require Admin` desvia para `admin`, `notAdmin`, `denied` ou `error`; `Restart as Admin` usa o prompt UAC normal quando confirmado.
- **Install App:** instala por `winget`, MSI, EXE ou URL direta com `dryRun`, timeout, retry, checksum e verificacao final.

Nunca desligue `dryRun` ou ligue `Allow System Changes` sem revisar origem, argumentos e impacto da acao.

### 5.8 Export Runner
O botao **Export Runner** gera um pacote semi-autonomo com:

- `Sidekick.exe` e dependencias copiadas do app atual
- `flow.json`
- `run-sidekick-flow.cmd`
- `README.txt`

Execute o `.cmd` para rodar aquele flow fora do editor visual. Limite atual: se uma instancia do Sidekick ja estiver aberta, o bloqueio de instancia unica pode impedir o runner.

### 5.9 Sample flows uteis
Os exemplos abaixo mostram o uso de ativos reutilizaveis no editor:

- `portfolio_snip_reuse_demo.json`: reaproveita um ativo de `Snip` em `Image Detected Trigger`.
- `portfolio_browser_mira_demo.json`: aplica um seletor do `Mira` em `Browser Wait Element` e `Browser Click`.
- `portfolio_browser_mira_text_demo.json`: aplica um seletor do `Mira` em `Browser Type` e `Browser Extract Text`.
- `recipe_desktop_automation.json`: captura/aplica seletor desktop e le texto.
- `recipe_wait_text_then_click.json`: espera texto e clica em um botao.
- `recipe_popup_auto_confirm.json`: clica automaticamente em um popup com cooldown/debounce/max repeat.
- `recipe_visual_fallback_click.json`: usa image matching quando selector nao basta.
- `recipe_scheduler_interval.json`: demonstra automacao por intervalo.
- `recipe_overlay_visual_message.json`: demonstra overlay de cor/texto em tela cheia.
- `recipe_console_pwd_command.json`: demonstra PWD, comando de console, stdout e log.
- `recipe_hardware_quick_controls.json`: demonstra leitura segura de audio, monitores e dispositivos.
- `trae_auto_continue.json`: flow oficial para Trae, usando trigger de elemento desktop, foco de janela e click protegido contra click storm.
- `recipe_screenshot_window_support.json`: captura screenshot por seletor de janela/processo.
- `recipe_desktop_recording.json`: gravacao desktop com duracao/fps e guard de tamanho.
- `recipe_camera_recording.json`: gravacao de camera com timestamp.
- `recipe_mira_resilient_click.json`: clique resiliente usando pipeline de fallback.
- `recipe_whatsapp_status_assistant.json`: assistente WhatsApp em modo seguro.
- `recipe_roblox_playtime_limit.json`: **Tempo de Jogo - ROBLOX**, controla 2 minutos de jogo com overlay, sons, fechamento e bloqueio ate 00:00.

### 5.10 Case oficial: Tempo de Jogo - ROBLOX
No Marketplace, abra **Tempo de Jogo - ROBLOX**.

O flow monitora `RobloxPlayerBeta`, `RobloxPlayerLauncher` e opcionalmente `RobloxStudioBeta`; inicia timer de 2 minutos; mostra overlays; toca sons do Windows; tenta fechar a janela com `Window Control / close`; usa `Kill Process` como fallback; salva estado local para bloquear novas aberturas ate `00:00`.

Antes de armar:

1. Revise se `RobloxStudioBeta` tambem deve contar como jogo.
2. Ajuste delays/textos se quiser outro limite.
3. Teste com **Run Now** em ambiente controlado.
4. Arme o flow somente quando os avisos e permissoes fizerem sentido para sua maquina.

Validacao atual: estrutura, nodes e protecoes do flow sao testadas automaticamente. A validacao real com Roblox instalado deve ser feita no ambiente do usuario.

### 5.8 Marketplace
Use o botao **Marketplace** na toolbar para abrir recipes oficiais locais e procurar por automacoes prontas incluidas no produto.

No executavel publicado, esses recipes sao copiados para `seed-flows/`. A pasta raiz `flows/` continua sendo a fonte editavel no repositorio.

Marketplace remoto e viavel, mas esta bloqueado como recurso publico ate existir manifesto seguro, hash/assinatura, validacao de schema, aviso de nodes sensiveis e importacao sempre desarmada.

Nesta versao, o Marketplace carrega apenas recipes oficiais locais. A avaliacao tecnica esta em `MARKETPLACE_EVALUATION.md`.

Sobre o recipe WhatsApp:

- abre `https://web.whatsapp.com/`
- exige login ativo (ou aguardo de QR scan)
- prepara rascunho por padrao (`draftOnly`)
- nao deve enviar automaticamente sem consentimento explicito
- envio sensivel exige `allowSendSensitiveData=true` e `sendMode=sendAfterConfirm`
- a UI do WhatsApp Web pode mudar; use fallback visual/selector e acompanhe logs.

---

## 6. Runtime continuo

### 6.1 O que a status bar mostra
- flow atual do editor
- flow em execucao agora
- quantidade de flows armados
- profundidade da fila
- ultimo trigger, ultima execucao e ultimo erro por flow
- fases semanticas como `waiting for element`, `element matched`, `fallback visual active`, `cooldown active` e `click executed`

### 6.2 Fila e coalescencia
Quando o mesmo flow dispara varias vezes em pouco tempo, o Sidekick mantem no maximo:

- 1 execucao atual
- 1 execucao pendente para esse mesmo flow

Isso evita reentrancia descontrolada e deixa o runtime previsivel.

### 6.3 Stop
Se houver fila pendente, o botao **Stop** abre uma escolha local:

- parar apenas a execucao atual
- parar a execucao atual e limpar a fila

---

## 7. Plugins

O Sidekick suporta nodes customizados via plugins.

### 7.1 Instalacao de plugin
1. Copie a DLL para `%AppData%\\Sidekick\\plugins\\`.
2. Reinicie o Sidekick.
3. Os novos nodes aparecerao automaticamente na palette.

### 7.2 Compatibilidade
Se voce vier de uma versao antiga que usava `%AppData%\\Ajudante`, o Sidekick migra os dados automaticamente na primeira execucao e preserva a pasta antiga como legado.

---

## 8. Dicas praticas

- Prefira triggers baseados em evento quando possivel.
- Use `Delay` entre acoes muito rapidas para deixar a automacao mais confiavel.
- Observe os logs da status bar ao depurar.
- Se um flow usa trigger continuo, lembre-se de **armar** o flow; executar manualmente nao deixa watchers ativos.

---

## 9. Solucao de problemas

| Problema | Solucao |
|----------|---------|
| App nao inicia | Verifique se o WebView2 Runtime esta instalado |
| Nodes nao aparecem | Reinicie o app para recarregar definicoes |
| Flow nao executa | Use **Run Now** para teste pontual ou **Arm** para monitoramento continuo |
| Hotkey nao funciona | Confirme que o flow esta armado |
| Trigger de pasta nao reage | Verifique o caminho, o filtro e o tipo de evento |
| Image match falha | Reduza o threshold ou recapture o template |
| Desktop selector nao encontra o alvo | Use **Test Selector**, confira `processName`, `processPath`, `windowTitleMatch` e recapture com Mira |
| App moderno nao expoe controles por UIAutomation | Use `Snip` + `Click Image Match` como fallback visual; OCR ainda nao esta pronto |
| Flow dispara vezes demais | Configure `cooldownMs`, `debounceMs` e `maxRepeat` no trigger desktop |

---

## 10. Onde ficam os arquivos

| O que | Onde |
|-------|------|
| Flows | `%AppData%\\Sidekick\\flows\\` |
| Logs | `%AppData%\\Sidekick\\logs\\` |
| Plugins | `%AppData%\\Sidekick\\plugins\\` |

Para abrir a pasta principal, pressione `Win+R`, digite `%AppData%\\Sidekick` e pressione Enter.
