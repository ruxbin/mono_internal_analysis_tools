using System;  
using System.Net;  
using System.Net.Sockets;  
using System.Text;
using System.Collections.Generic;

public enum PoolType
{
    PT_STRING,
	PT_METHOD,
	PT_KLASS,
	PT_OPTYPE,
	PT_INPUTTYPE,
	PT_ENUMKLASS,
	PT_SIGNATURE
}
public enum MessageType
{
BEGIN_GROUP = 0x00,
BEGIN_GRAPH = 0x01,
CLOSE_GROUP = 0x02,

POOL_NEW = 0x00,
POOL_STRING = 0x01,
POOL_ENUM = 0x02,
POOL_KLASS = 0x03,
POOL_METHOD = 0x04,
POOL_NULL = 0x05,
POOL_NODE_CLASS = 0x06,
POOL_FIELD = 0x07,
POOL_SIGNATURE = 0x08,

PROPERTY_POOL = 0x00,
PROPERTY_INT = 0x01,
PROPERTY_LONG = 0x02,
PROPERTY_DOUBLE = 0x03,
PROPERTY_FLOAT = 0x04,
PROPERTY_TRUE = 0x05,
PROPERTY_FALSE = 0x06,
PROPERTY_ARRAY = 0x07,
PROPERTY_SUBGRAPH = 0x08

}

[Serializable]
public class DataCorruption : Exception
{
    public DataCorruption(string message) : base(message) { }
}
public class  EnumClass
{
    private string inputType;
    private string fixed_;

    public EnumClass(string i,string f)
    {
        this.inputType = i;
        this.fixed_ = f;
    }
}
public class InputType
{
    private EnumClass enumClass;

    public InputType(EnumClass e)
    {
        this.enumClass = e;
    }
}

public class Instruction
{
    private string opcode;
    private string insDesc;
    private string predecessor;

    private InputType inputType;

    private string[] successors;

    public Instruction(string op,string desc,string pred,InputType i,List<string> su)
    {
        this.opcode = op;
        this.insDesc = desc;
        this.predecessor = pred;
        this.inputType = i;
        this.successors = su.ToArray();
    }
}
public class MethodSig
{
    private short paramCount;
    private string[] paramTypeDesc;

    private string retTypeDesc;


    public MethodSig(short paramCount,List<string> param,string retdesc)
    {
        this.paramCount = paramCount;
        this.paramTypeDesc = param.ToArray();
        this.retTypeDesc = retdesc;
    }
}
public class ClassSig
{
    private string className;
    public ClassSig(string s)
    {
        this.className = s;
    }
}

public class Method
{
    private ClassSig classSig;
    private string name;

    private MethodSig methodSig;

    public Method(ClassSig csig,string name,MethodSig msig)
    {
        this.classSig = csig;
        this.name = name;
        this.methodSig = msig;
    }

}

public class BeginGroupMessage
{
    private string title;
    private string methodName;

    private Method method;

    public void ParseFromSocket(Socket s)
    {
        this.title = (string)DumpServer.ReadFromPool(s);
        this.methodName = (string)DumpServer.ReadFromPool(s);
        this.method = (Method)DumpServer.ReadFromPool(s);
        int zero = DumpServer.ReadInt(s);
        if(zero!=0)
        {
            throw new DataCorruption("Data corrupted should be zero");
        }
    }
}

public class InstructionInfo
{
    private Instruction instruction;
    private byte isEntry;

    private string property1name;

    private byte property1_pool;

    private string insDesc;

    private string property2_category;

    private byte property2_pool;

    private string propertyString;
    
    private int[] successors;

    public InstructionInfo(Instruction ins,byte isentry,string property1name,byte property1p,string insdesc,
                    string property2category,byte property2p,string propertystring,int[] s)
    {
        this.instruction = ins;
        this.isEntry = isentry;
        this.property1name = property1name;
        this.property1_pool = property1p;
        this.insDesc = insdesc;
        this.property2_category = property2category;
        this.property2_pool = property2p;
        this.propertyString = propertystring;
        this.successors = s;
    }

}

public class Block
{
    private int blockNum;
    //private int insnSize;

    private int[] insidIndices;

    private int[] outBlockNums;

    public Block(int bn,int[] ins, int[] outs)
    {
        this.blockNum = bn;
        this.insidIndices = ins;
        this.outBlockNums = outs;
    } 
}
public class Blocks
{
    Block[] blocks;
    public void ParseFromSocket(Socket s)
    {
        int blockSize = DumpServer.ReadInt(s);
        blocks = new Block[blockSize];
        for(int i=0;i<blockSize;++i)
        {
            int blocknum = DumpServer.ReadInt(s);
            int insn_size = DumpServer.ReadInt(s);
            int[] insids = new int[insn_size];
            for(int j=0;j<insn_size;++j)
            {
                insids[j] = DumpServer.ReadInt(s);
            }
            int outs_size = DumpServer.ReadInt(s);
            int[] outbbs = new int[outs_size];
            for(int j=0;j<outs_size;++j)
            {
                outbbs[j] = DumpServer.ReadInt(s);
            }
            blocks[i] = new Block(blocknum,insids,outbbs);
        }
    }
}

public class Instructions
{
    private int instructionCount;

    const int NUM_SUCCESSOR = 5;

    Dictionary<int,InstructionInfo> insid2Info = new Dictionary<int, InstructionInfo>();

    public void ParseFromSocket(Socket s)
    {
        this.instructionCount = DumpServer.ReadInt(s);
        for(int i=0;i<this.instructionCount;++i)
        {
            int id = DumpServer.ReadInt(s);
            Instruction instruction = (Instruction)DumpServer.ReadFromPool(s);
            byte isEntry = DumpServer.ReadByte(s);

            short numOfProperties = DumpServer.ReadShort(s);
            if(numOfProperties!=2)
            {
                throw new DataCorruption(string.Format("data corrupted num of properties should be 2 instead of {0}",numOfProperties));
            }
            //property #1
            string fullname = (string)DumpServer.ReadFromPool(s);
            byte property_pool = DumpServer.ReadByte(s);
            if(property_pool!=(byte)MessageType.PROPERTY_POOL)
            {
                throw new DataCorruption(string.Format("data corrupted num of properties should be PROPERTY_POOL instead of {0}",property_pool));
            }
            string insnDesc = (string)DumpServer.ReadFromPool(s);
            //property #2
            string category = (string)DumpServer.ReadFromPool(s);
            byte property2_pool = DumpServer.ReadByte(s);
            if(property2_pool!=(byte)MessageType.PROPERTY_POOL)
            {
                throw new DataCorruption(string.Format("data corrupted! num of properties should be PROPERTY_POOL instead of {0}",property2_pool));
            }
            //"mege","begin","controlSplit","phi","state","fixed"
            string propertyString = (string)DumpServer.ReadFromPool(s);
            int negativeOne = DumpServer.ReadInt(s);
            if(negativeOne!=-1)
            {
                throw new DataCorruption(string.Format("data corrupted should be -1 instead of {0}",negativeOne));
            }
            int[] successors = new int[5];
            for(int j=0;j<Instructions.NUM_SUCCESSOR;++j)
            {
                int successor = DumpServer.ReadInt(s);
                successors[j] = successor;
            }
            this.insid2Info.Add(id,new InstructionInfo(instruction,isEntry,fullname,property_pool,insnDesc,category,property2_pool,propertyString,successors));
        }
        
    }
}


public class IRMessage
{
    private string phaseName;
    private Instructions instructions;

    private Blocks blocks;
    public void ParseFromSocket(Socket s)
    {
        this.phaseName = (string)DumpServer.ReadFromPool(s);
        Console.WriteLine(this.phaseName);
        Instructions ins = new Instructions();
        ins.ParseFromSocket(s);
        Blocks blocks = new Blocks();
        blocks.ParseFromSocket(s);
        this.instructions = ins;
        this.blocks = blocks;
    }
}

public class DumpServer
{

    //static List<byte> rawbyte = new List<byte>();

    public static Dictionary<short,Object> constantPool = new Dictionary<short, object>();

    //TODO:optimize
    public static byte[] ReceiveBytes(int remaining,Socket s)
    {
        List<byte> res = new List<byte>();
        while(remaining>0)
        {
            byte[] rawbytes = new byte[remaining];
            int bytesRead = s.Receive(rawbytes,remaining,SocketFlags.None);
            remaining -= bytesRead;
            for(int i=0;i<bytesRead;++i)
            {
                res.Add(rawbytes[i]);
            }
        }
        return res.ToArray();
    }
    public static byte ReadByte(Socket s)
    {
        // byte[] rawbytes = new byte[1];
        // int bytes = s.Receive(rawbytes);
        // if(bytes!=1)
        // {
        //     throw new DataCorruption(string.Format("data corrupted receive:{0}-expected{1}",bytes,1));
        // }
        byte[] rawbytes = ReceiveBytes(1,s);
        return rawbytes[0];
    }

    public static short ReadShort(Socket s)
    {
        // byte[] rawbytes = new byte[2];
        // int bytes = s.Receive(rawbytes);
        // if(bytes!=2)
        // {
        //     throw new DataCorruption(string.Format("data corrupted receive:{0}-expected{1}",bytes,2));
        // }
        byte[] rawbytes = ReceiveBytes(2,s);
        //rawbyte.ToArray->byte[]->ReadOnlySpan??
        return IPAddress.NetworkToHostOrder(BitConverter.ToInt16(rawbytes,0));
    }

    
    public static int ReadInt(Socket s)
    {
        // byte[] rawbytes = new byte[4];
        // int bytes = s.Receive(rawbytes);
        // if(bytes!=4)
        // {
        //     throw new DataCorruption(string.Format("data corrupted receive:{0}-expected{1}",bytes,4));
        // }
        byte[] rawbytes = ReceiveBytes(4,s);
        return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(rawbytes,0));
    }

    public static string ReadString(Socket s)
    {
        int len = ReadInt(s);
        // byte[] rawbytes = new byte[len*2];
        // int bytes = s.Receive(rawbytes,len*2,SocketFlags.None);
        byte[] rawbytes = ReceiveBytes(len*2,s);
        for(int i=0;i<len;++i)
        {
            //byte[] vv = BitConverter.GetBytes(IPAddress.NetworkToHostOrder(BitConverter.ToInt16(rawbytes,i*2)));
            //rawbytes[i*2] = vv[0];
            //rawbytes[i*2+1] = vv[1];
            byte temp = rawbytes[i*2];
            rawbytes[i*2] = rawbytes[i*2+1];
            rawbytes[i*2+1] = temp;
        }
        UnicodeEncoding encoding = new UnicodeEncoding();
        // if(bytes!=len*2)
        // {
        //     throw new DataCorruption(string.Format("data corrupted receive:{0}-expected{1}-->{2}",bytes,len*2,encoding.GetString(rawbytes)));
        // }
        
        return encoding.GetString(rawbytes);
    }

    //the inverse of write_pool
    public static Object ReadFromPool(Socket s)
    {
        MessageType type = (MessageType)ReadByte(s);
        
        if(type==MessageType.POOL_NULL)
            return null;
        switch(type){
            //inverse of add_pool_entry
            case MessageType.POOL_NEW:
                short id = ReadShort(s);
                MessageType actualType = (MessageType)ReadByte(s);
                switch(actualType){
                    case MessageType.POOL_STRING://PoolType.PT_STRING:
                        string sval = ReadString(s);
                        constantPool.Add(id,sval);
                        break;

                    case MessageType.POOL_METHOD://PoolType.PT_METHOD:
                        var classobj = (ClassSig)ReadFromPool(s);
                        string methodname = (string)ReadFromPool(s);
                        var methoddsig = (MethodSig)ReadFromPool(s);
                        /*int methodFlags = */ReadInt(s);
                        int mustNegativeOne = ReadInt(s);
                        if(mustNegativeOne!=-1)
                        {
                            throw new DataCorruption("data corrupted must end with -1");
                        }
                        constantPool.Add(id,new Method(classobj,methodname,methoddsig));
                        break;

                    case MessageType.POOL_KLASS://PoolType.PT_KLASS: PoolType.PT_ENUMKLASS
                        string className = ReadString(s);
                        MessageType classOrEnum = (MessageType)ReadByte(s);
                        if(classOrEnum==MessageType.POOL_ENUM)
                        {
                            /*int one = */ReadInt(s);
                            //TODO:check
                            string fixed_ = (string)ReadFromPool(s);
                            constantPool.Add(id,new EnumClass(className,fixed_));
                        }
                        else
                        {
                            constantPool.Add(id,new ClassSig(className));
                        }
                        
                        break;

                    case MessageType.POOL_SIGNATURE://PoolType.PT_SIGNATURE:
                        short paramCount = ReadShort(s);
                        List<string> paramsDescs = new List<string>();
                        for(int i=0;i<paramCount;++i)
                        {
                            string paramDesc = (string)ReadFromPool(s);
                            paramsDescs.Add(paramDesc);
                        }
                        string retDesc = (string)ReadFromPool(s);
                        constantPool.Add(id,new MethodSig(paramCount,paramsDescs,retDesc));
                        break;

                    case MessageType.POOL_NODE_CLASS://PoolType.PT_OPTYPE:
                        string instName = ReadString(s);
                        string instDesc = ReadString(s);
                        short one_ = ReadShort(s);
                        if(one_!=1)
                        {
                            throw new DataCorruption(string.Format("data corrupted: should be 1 instead of {0}",one_));
                        }
                        var zero = ReadByte(s);
                        if(zero!=0)
                        {
                            throw new DataCorruption(string.Format("data corrupted: should be 0 instead of {0}",zero));
                        }
                        string predecessor = (string)ReadFromPool(s);
                        var inputType =(InputType) ReadFromPool(s);
                        short numSuccessor = ReadShort(s);
                        List<string> successors = new List<string>();
                        for(int i=0;i<numSuccessor;++i)
                        {
                            byte zero_ = ReadByte(s);
                            if(zero_!=0)
                            {
                                throw new DataCorruption(string.Format("data corrupted!: should be 0 instead of {0}",zero_));
                            }
                            string successorName = (string)ReadFromPool(s);
                            successors.Add(successorName);
                        }
                        constantPool.Add(id,new Instruction(instName,instDesc,predecessor,inputType,successors));
                        break;

                    case MessageType.POOL_ENUM://PoolType.PT_INPUTTYPE:
                        var enumclass = (EnumClass)ReadFromPool(s);
                        int zero__ = ReadInt(s);
                        if(zero__!=0)
                        {
                            throw new DataCorruption("data corrupted must end with 0");
                        }
                        constantPool.Add(id,new InputType(enumclass));
                        break;

                    default:
                        throw new DataCorruption(string.Format("data corrupted unknown pool type:{0}",actualType));
                        break;
                }
                return constantPool[id];

            case MessageType.POOL_STRING:
            case MessageType.POOL_METHOD:
            case MessageType.POOL_KLASS:
            case MessageType.POOL_SIGNATURE:
            case MessageType.POOL_NODE_CLASS:
            case MessageType.POOL_ENUM:
                short idExist = ReadShort(s);
                return constantPool[idExist];

            default:
                Console.WriteLine("datea corrupted unknown message type: {0}",type);
            break;
        }
        return null;
    }

    public static void Main()
    {
        IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 4445); 
        // Create a TCP/IP socket.  
        Socket listener = new Socket(ipAddress.AddressFamily,  
            SocketType.Stream, ProtocolType.Tcp );
        //byte[] bytes = new Byte[1024];

        try {  
            listener.Bind(localEndPoint);  
            listener.Listen(10);  
  
            // Start listening for connections.  
            while (true) {  
                Console.WriteLine("Waiting for a connection...");  
                // Program is suspended while waiting for an incoming connection.  
                Socket handler = listener.Accept();  
                Console.WriteLine("connected");
  
                // An incoming connection needs to be processed.
                Boolean exit = false;  
                while (!exit) {  
                    //int bytesRec = handler.Receive(bytes);  
                    //Console.WriteLine("{0} bytes received",bytesRec);

                    //BeginGroup
                    MessageType msgType = (MessageType)ReadByte(handler);
                    switch(msgType)
                    {
                        case MessageType.BEGIN_GROUP:
                            BeginGroupMessage begin = new BeginGroupMessage();
                            begin.ParseFromSocket(handler);
                            break;

                        case MessageType.CLOSE_GROUP:
                            exit = true;
                            DumpServer.constantPool.Clear();
                            Console.WriteLine("CLOSE_GROUP received, ready to exit");
                            break;

                        case MessageType.BEGIN_GRAPH:
                            IRMessage irmsg = new IRMessage();
                            irmsg.ParseFromSocket(handler);
                            break;
                        
                        default:
                            throw new DataCorruption(string.Format("data corrput unknown msgtype {0}",msgType));
                            exit = true;
                            break;
                    }

                    //EndGroup

                }  
                
                handler.Close();  
            }  
  
        } catch (Exception e) {  
            Console.WriteLine(e.ToString());  
        }  
    }
}