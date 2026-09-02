# SectorFlow LMU Network Analyzer

Ferramenta gratuita para Windows focada em diagnosticar problemas de rede no **Le Mans Ultimate (LMU)**, especialmente desconexões na transição de Practice para Race e instabilidade em conexões como Starlink.

## O que esta primeira versão faz

- Detecta o processo do Le Mans Ultimate quando ele está aberto.
- Procura o diretório de logs do LMU em instalações Steam comuns.
- Tenta encontrar IPs públicos presentes nos logs recentes.
- Permite informar manualmente o IP/host do Race Server.
- Monitora ping continuamente.
- Calcula jitter aproximado.
- Calcula percentual de respostas ICMP perdidas.
- Executa traceroute.
- Faz teste aproximado de Path MTU IPv4.
- Procura linhas recentes de disconnect/timeout nos logs do LMU.
- Exporta relatório TXT + CSV em:
  - `Documentos\SectorFlowLMU\Reports`
- Pode colocar o processo do LMU em prioridade `AboveNormal`.
- Inclui uma política QoS opcional para Windows usando DSCP 46.
- Gera executável Windows x64 automaticamente pelo GitHub Actions.

## Segurança / anti-cheat

O programa foi desenhado para **não interferir com o Easy Anti-Cheat**:

- não injeta DLL;
- não lê/escreve memória do jogo;
- não modifica o executável;
- não intercepta nem altera pacotes do LMU;
- não tenta burlar firewall, autenticação ou anti-cheat;
- não instala VPN automaticamente.

Ele apenas observa a rede do Windows, executa diagnósticos padrão e aplica otimizações locais opcionais.

## Limitação importante

Esta versão **não consegue obrigar a Starlink ou outro ISP a mudar a rota BGP internacional**.

Para realmente trocar a rota externa seria necessário um relay/túnel, como WireGuard/VPS. Isso será estudado depois de termos dados reais das corridas, para evitar repetir problemas semelhantes ao Cloudflare WARP.

## ICMP não é o tráfego do jogo

Ping e traceroute usam ICMP. Alguns servidores e roteadores bloqueiam ou limitam ICMP.

Portanto:

- 100% de perda no ping não prova que o servidor LMU está offline;
- uma rota com `*` no traceroute não prova que aquele salto caiu;
- os resultados devem ser correlacionados com o horário real da desconexão e com os logs do LMU.

## Como testar

1. Abra o Le Mans Ultimate.
2. Abra o SectorFlow LMU Network Analyzer.
3. Clique em **Detect LMU**.
4. Entre em uma sessão Practice.
5. Clique em **Discover server from logs**.
6. Quando for transferido para o Race Server, repita a descoberta.
7. Se necessário, coloque manualmente o IP do Race Server no campo Target.
8. Clique em **Start monitor**.
9. Deixe o monitor funcionando durante a corrida.
10. Se desconectar, clique em **Export report** imediatamente após o problema.

## Como baixar o executável

Abra a aba **Actions** do repositório, entre no workflow mais recente **Build Windows App** e baixe o artifact:

`SectorFlow-LMU-NetworkAnalyzer-win-x64`

O build é self-contained para Windows x64, então não deve exigir instalação separada do .NET 8.

## Compilar manualmente

Requer .NET 8 SDK no Windows.

```powershell
dotnet restore src/SectorFlow.LmuNetworkAnalyzer/SectorFlow.LmuNetworkAnalyzer.csproj
dotnet build src/SectorFlow.LmuNetworkAnalyzer/SectorFlow.LmuNetworkAnalyzer.csproj -c Release
dotnet publish src/SectorFlow.LmuNetworkAnalyzer/SectorFlow.LmuNetworkAnalyzer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## QoS opcional

O botão **Install LMU QoS** executa, como Administrador:

`scripts/Apply-LmuQoS.ps1`

A regra usa o nome do executável:

`Le Mans Ultimate.exe`

e marca o tráfego com DSCP 46 quando o Windows/equipamentos de rede respeitam essa marcação.

Isso pode ajudar em congestionamento **local**, mas não muda a rota internacional da Starlink.

Para remover:

`scripts/Remove-LmuQoS.ps1`

## Próximas etapas planejadas

- detectar melhor endpoints UDP/RUDP usados pelo LMU;
- registrar automaticamente a troca Practice -> Race;
- correlacionar desconexão do jogo com mudança de rota;
- monitorar interface de rede/Starlink em paralelo;
- comparar IPv4 e IPv6;
- classificar qualidade da rota com score;
- estudar relay WireGuard gratuito/opcional somente se os dados mostrarem benefício real.

## Aviso

Projeto experimental. Faça os testes primeiro em Practice e sessões sem importância antes de usar durante uma corrida oficial.
