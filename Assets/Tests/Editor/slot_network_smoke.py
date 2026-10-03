import socket,struct,base64,os,time,json,uuid,select
from pathlib import Path

def mid(name):
    h=2166136261
    for b in name.encode(): h=((h^b)*16777619)&0xffffffff
    return ((h>>16)^h)&65535

def s(x):
    if x is None:return b'\0\0'
    v=x.encode();return struct.pack('<H',len(v)+1)+v

class Reader:
    def __init__(self,b):self.b=b;self.p=0
    def take(self,n):v=self.b[self.p:self.p+n];self.p+=n;return v
    def num(self,f):return struct.unpack('<'+f,self.take(struct.calcsize('<'+f)))[0]
    def string(self):
        n=self.num('H');return self.take(n-1).decode() if n else None
    def var(self):
        a=self.num('B')
        if a<241:return a
        if a<249:return 240+(a-241)*256+self.num('B')
        if a==249:return 2288+self.num('B')*256+self.num('B')
        return int.from_bytes(self.take(a-247),'little')

def var(n):
    if n<=240:return bytes([n])
    if n<=2287:return bytes([((n-240)>>8)+241,(n-240)&255])
    if n<=67823:return bytes([249])+struct.pack('>H',n-2288)
    return bytes([250])+n.to_bytes(3,'little')

class Peer:
    def __init__(self,name):
        self.name=name;self.q=[];self.seq=0
        self.sock=socket.create_connection(('127.0.0.1',7777));self.sock.settimeout(8)
        key=base64.b64encode(os.urandom(16)).decode()
        self.sock.sendall(('GET / HTTP/1.1\r\nHost: 127.0.0.1:7777\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: '+key+'\r\nSec-WebSocket-Version: 13\r\n\r\n').encode())
        header=b''
        while not header.endswith(b'\r\n\r\n'):header+=self.sock.recv(1)
        assert b'101' in header,header
    def exact(self,n):
        b=b''
        while len(b)<n:
            v=self.sock.recv(n-len(b))
            if not v:raise RuntimeError('closed')
            b+=v
        return b
    def frame(self,data,opcode=2):
        n=len(data);mask=os.urandom(4)
        head=bytes([128|opcode,128|n]) if n<126 else bytes([128|opcode,254])+struct.pack('>H',n)
        self.sock.sendall(head+mask+bytes(b^mask[i%4] for i,b in enumerate(data)))
    def send(self,name,payload=b'',namespace='Game.Network'):
        msg=struct.pack('<H',mid(namespace+'.'+name))+payload
        self.frame(struct.pack('<d',time.monotonic())+var(len(msg))+msg)
    def receive(self):
        a,b=self.exact(2);n=b&127
        if n==126:n=struct.unpack('>H',self.exact(2))[0]
        if n==127:n=struct.unpack('>Q',self.exact(8))[0]
        mask=self.exact(4) if b&128 else None
        data=self.exact(n)
        if mask:data=bytes(x^mask[i%4] for i,x in enumerate(data))
        if a&15==8:raise RuntimeError('server closed')
        if a&15==9:self.frame(data,10);return
        if a&15!=2:return
        r=Reader(data);r.take(8)
        while r.p<len(data):
            msg=Reader(r.take(r.var()));self.q.append((msg.num('H'),msg.take(len(msg.b)-msg.p)))
    def wait(self,name,timeout=10):
        target=mid('Game.Network.'+name);end=time.monotonic()+timeout
        while time.monotonic()<end:
            for i,(id,b) in enumerate(self.q):
                if id==target:self.q.pop(i);return Reader(b)
            if select.select([self.sock],[],[],0.1)[0]:self.receive()
        raise TimeoutError(name+' queued='+str([id for id,b in self.q]))
    def auth(self,register=True):
        self.send('AccountRegisterRequest' if register else 'AccountLoginRequest',s(self.name)+s('SlotTest_123!'))
        r=self.wait('AccountAuthenticationResponse');r.num('B');code=r.var();assert code==0,code
        self.account=r.string();r.string();self.chips=r.var();self.dollars=r.var()
    def slot(self,action=0,op=None,rev=None):
        time.sleep(.18);self.seq+=1
        self.send('SlotLotteryRequest',var(self.seq)+bytes([action])+s(op)+bytes([0])+var(0)+s(rev)+bytes([1]))
        r=self.wait('SlotLotteryResponse');rid=r.var();account=r.string();act=r.num('B');result=r.num('B');revision=r.string();enabled=r.num('B');cost=r.var();dollars=r.var();chips=r.var();available=r.var();can=r.num('B');receipt=json.loads(r.string());disclosure=json.loads(r.string())
        assert rid==self.seq and account==self.account
        return dict(result=result,revision=revision,cost=cost,dollars=dollars,chips=chips,receipt=receipt)
    def close(self):
        try:self.frame(b'',8)
        except OSError:pass
        self.sock.close()

def run():
    tag=uuid.uuid4().hex[:6];a=Peer('slotA'+tag);b=Peer('slotB'+tag)
    peers=[a,b];report=[]
    try:
        for p in peers:p.auth()
        report.append('Two independent WebSocket clients authenticated')
        q=a.slot();assert q['cost']==100
        op=uuid.uuid4().hex;result=a.slot(1,op,q['revision']);assert result['result']==0
        repeat=a.slot(1,op,q['revision']);assert repeat['receipt']==result['receipt'] and repeat['dollars']==result['dollars'] and repeat['chips']==result['chips']
        assert b.slot()['receipt'] is None
        report.append('Paid draw, replay idempotency and account isolation passed')
        a.close();time.sleep(.4);a=Peer(a.name);peers[0]=a;a.auth(False)
        restored=a.slot();assert restored['receipt']==result['receipt']
        assert a.slot(2,op)['receipt'] is None
        report.append('Disconnect, login and pending receipt recovery passed')
        for p in peers:
            p.send('WalletRequest',var(1)+bytes([1])+s(uuid.uuid4().hex)+bytes([1])+var(2000))
            r=p.wait('WalletResponse');r.var();r.string();r.num('B');r.string();assert r.num('B')==0
        a.send('PlayerCreateRoomRequest',s(a.name));r=a.wait('RoomInfoSync');r.num('B')
        room=json.loads(r.string());roomid=room['Id']
        b.send('PlayerJoinRoomRequest',s(b.name)+var(roomid*2));r=b.wait('PlayerJoinRoomResponse');assert r.var()==0
        for p in peers:p.send('PlayerChangeReadyMessage',s(p.name)+var(roomid*2),namespace='Assets.Scripts.Network.Messages')
        time.sleep(.4);a.send('PlayerStartGameRequest',s(a.name)+var(roomid*2))
        stages=set();turns=set();bet=False;settled=False;end=time.monotonic()+35
        while time.monotonic()<end and not settled:
            for p in peers:
                if select.select([p.sock],[],[],.03)[0]:p.receive()
                for id,payload in list(p.q):
                    if id==mid('Game.Network.RoundResultNotification'):
                        settled=True;report.append('Round settlement received')
                    if id!=mid('Game.Network.GameInfoSync'):continue
                    p.q.remove((id,payload));info=json.loads(Reader(payload).string())
                    stages.add(info['Round']);turn=info['TurnId']
                    if info['OperatingPlayer']!=info['CurrentPlayerIndex'] or turn in turns:continue
                    turns.add(turn);idx=info['CurrentPlayerIndex'];player=info['Players'][idx]
                    owed=info['CurrentHighestPlayerBet']-player['StageBet']
                    action=3 if owed else 0;number=0
                    if not owed and not bet:action=1;number=max(100,info['CurrentHighestRaise']);bet=True
                    p.send('PlayerOperationRequest',var(roomid*2)+s(info['HandId'])+var(turn)+var(idx*2)+var(action*2)+var(number))
        assert settled,('no settlement',stages,turns)
        assert len(stages)>=4,stages
        report.append('Create/join/ready/bet/call/check and street progression passed: '+str(sorted(stages)))
        print(json.dumps(report,ensure_ascii=False),flush=True)
        Path('Logs/SlotTests/network-result.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    finally:
        for p in peers:p.close()

if __name__=='__main__':run()
