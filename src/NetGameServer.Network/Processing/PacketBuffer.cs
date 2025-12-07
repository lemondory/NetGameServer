using System.Buffers;
using System.Collections.Concurrent;

namespace NetGameServer.Network.Processing;

// 분할 수신된 패킷을 재조립하는 버퍼
public class PacketBuffer : IDisposable
{
    private readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;
    private byte[] _buffer;
    private int _bufferOffset = 0;
    private readonly int _initialSize;
    private readonly int _maxSize;
    private readonly object _lock = new();
    
    public PacketBuffer(int initialSize = 4096, int maxSize = 1024 * 1024) // 1MB
    {
        _initialSize = initialSize;
        _maxSize = maxSize;
        _buffer = _arrayPool.Rent(initialSize);
    }
    
    public void Append(byte[] data, int length)
    {
        lock (_lock)
        {
            EnsureCapacity(_bufferOffset + length);
            Buffer.BlockCopy(data, 0, _buffer, _bufferOffset, length);
            _bufferOffset += length;
        }
    }
    
    public List<byte[]> ExtractCompletePackets()
    {
        var packets = new List<byte[]>();
        
        lock (_lock)
        {
            while (_bufferOffset >= sizeof(int))
            {
                var packetSize = BitConverter.ToInt32(_buffer, 0);
                
                if (packetSize < 0 || packetSize > _maxSize)
                {
                    Console.WriteLine($"잘못된 패킷 크기: {packetSize}, 버퍼 초기화");
                    _bufferOffset = 0;
                    break;
                }
                
                var totalSize = sizeof(int) + packetSize;
                
                if (_bufferOffset < totalSize)
                {
                    break;
                }
                
                var packetData = new byte[packetSize];
                Buffer.BlockCopy(_buffer, sizeof(int), packetData, 0, packetSize);
                packets.Add(packetData);
                
                if (_bufferOffset > totalSize)
                {
                    Buffer.BlockCopy(_buffer, totalSize, _buffer, 0, _bufferOffset - totalSize);
                }
                _bufferOffset -= totalSize;
            }
        }
        
        return packets;
    }
    
    private void EnsureCapacity(int requiredSize)
    {
        if (requiredSize <= _buffer.Length)
            return;
            
        var newSize = Math.Min(_buffer.Length * 2, _maxSize);
        if (newSize < requiredSize)
        {
            throw new InvalidOperationException($"패킷 버퍼 최대 크기 초과: {requiredSize} > {_maxSize}");
        }
        
        var newBuffer = _arrayPool.Rent(newSize);
        Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _bufferOffset);
        _arrayPool.Return(_buffer);
        _buffer = newBuffer;
    }
    
    public void Clear()
    {
        lock (_lock)
        {
            _bufferOffset = 0;
        }
    }
    
    public void Dispose()
    {
        lock (_lock)
        {
            _arrayPool.Return(_buffer);
            _buffer = Array.Empty<byte>();
        }
    }
}

