# Catálogo de salas LAN e públicas

O lobby procura hosts LAN automaticamente por UDP 47776. Ao criar uma sala,
ela responde à descoberta enquanto aceita jogadores. A lista atualiza a cada
3 segundos; anúncios LAN ausentes expiram após 10 segundos. Não é necessário
um serviço para LAN. Permita a descoberta e a porta KCP do jogo no firewall.

Salas públicas usam este catálogo HTTP próprio, sem dependências Python externas.
Ele **não é um relay**: o jogador conecta diretamente ao host pela porta UDP KCP
(7777 no padrão da cena). O host precisa encaminhar essa porta no roteador e
permiti-la no firewall. CGNAT exige IP público, VPN apropriada ou um relay futuro.
A lista não comprova que a porta do host está acessível.

## Teste local

```powershell
cd Tools/RoomDirectory
python -m unittest -v
python server.py
```

Em `Assets/Resources/OnlineRoomDirectory.json`, configure temporariamente:

```json
{ "directoryUrl": "http://127.0.0.1:8080" }
```

Reconstrua os clientes após configurar. No lobby, alterne `VISIBILIDADE: LAN`
para `VISIBILIDADE: PÚBLICA` **antes** de criar a sala. A opção pública só fica
habilitada com URL configurada. O anúncio público renova a cada 5 segundos;
salas fechadas, cheias ou em configuração são removidas e deixam de renovar.
Hosts encerrados abruptamente expiram em até 20 segundos. Voltar ao lobby
com vagas anuncia a sala novamente. IP manual continua disponível.

## Hospedagem pública

Use Python 3.12+ em um servidor com domínio e HTTPS. Execute atrás de um
reverse proxy local que sobrescreva `X-Real-IP` com o IP real da conexão:

```sh
python server.py --bind 127.0.0.1 --port 8080 --trust-proxy
```

Exemplo do bloco Nginx dentro do virtual host HTTPS do seu domínio:

```nginx
location / {
    proxy_pass http://127.0.0.1:8080;
    proxy_set_header X-Real-IP $remote_addr;
    client_max_body_size 2k;
}
```

Configure certificado TLS e limites de requisições/conexões no proxy conforme
a hospedagem. Não exponha o backend com `--trust-proxy` diretamente à internet:
ele confia no IP recebido do proxy. Se usar Docker, publique somente em loopback:

```sh
docker build -t sb6-room-directory Tools/RoomDirectory
docker run -d --restart unless-stopped -p 127.0.0.1:8080:8080 sb6-room-directory
```

Configure a URL HTTPS final no JSON e gere os builds que todos usarão.
A URL vem vazia no repositório porque nenhum serviço foi hospedado ainda.
Não use `localhost` para builds distribuídos. HTTP sem TLS é permitido somente
para loopback em testes. O catálogo determina o IP público pelo remetente HTTP,
não aceita um endereço arbitrário anunciado pelo cliente, e usa tokens privados
por sala para renovação/remoção. O token não aparece na lista. Há limite de
500 salas, oito por IP e uma renovação por segundo. Dados ficam em memória;
reiniciar o serviço limpa a lista até os hosts renovarem.

## Validação entre clientes

1. Gere um cliente atualizado e abra o Editor no mesmo projeto.
2. Crie sala LAN no executável; entre no OnlineLobby no Editor e selecione a sala.
3. Confira jogadores/slots e configuração de partida; confira que a sala some
   da lista de um terceiro cliente ao ficar cheia ou iniciar configuração.
4. Feche o host e confira a expiração da sala na lista.
5. Com serviço configurado, repita usando visibilidade pública. Para validar
   acesso pela internet, use duas redes diferentes e encaminhe a porta UDP.
6. Desligue o catálogo; confira aviso público e funcionamento da lista LAN.
