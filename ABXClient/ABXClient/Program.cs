// See https://aka.ms/new-console-template for more information
using ABXClient;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;


class Program
{
    // Server details
    private const string Server = "localhost";
    private const int Port = 3000;
    private const string OutputFile = "packets.json";

    static void Main(string[] args)
    {
        try
        {
            // Step 1: Get initial packets from the server
            Console.WriteLine("Connecting to server to get packets...");
            var packets = ReceivePackets();

            // Step 2: Find and get missing packets
            Console.WriteLine("Checking for missing packets...");
            packets = RequestMissingPackets(packets);

            // Step 3: Sort packets by sequence number
            packets.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
            Console.WriteLine($"Total packets: {packets.Count}");

            // Step 4: Save packets to a JSON file
            SaveToJson(packets);
            Console.WriteLine($"Saved packets to {OutputFile}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Something went wrong: {ex.Message}");
        }
    }

    // Connect to server and receive packets
    static List<Packet> ReceivePackets()
    {
        var packets = new List<Packet>();
        using var client = new TcpClient(Server, Port);
        using var stream = client.GetStream();

        // Send Call Type 1 (byte with value 1)
        stream.WriteByte(1);
        stream.Flush(); // Make sure the byte is sent

        // Keep reading packets until the server stops
        byte[] buffer = new byte[17]; // Each packet is 17 bytes
        while (true)
        {
            try
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break; // Server disconnected
                if (bytesRead != 17)
                {
                    Console.WriteLine($"Got weird packet size: {bytesRead} bytes");
                    continue;
                }

                // Turn the 17 bytes into a Packet object
                Packet packet = ParsePacket(buffer);
                packets.Add(packet);
                Console.WriteLine($"Got packet: Seq={packet.Sequence}, Symbol={packet.Symbol}, BuySell={packet.BuySell}");
            }
            catch (IOException)
            {
                // Server probably disconnected
                break;
            }
        }

        return packets;
    }

    // Turn 17 bytes into a Packet
    static Packet ParsePacket(byte[] buffer)
    {
        return new Packet
        {
            Symbol = Encoding.ASCII.GetString(buffer, 0, 4).Trim(), // First 4 bytes = symbol
            BuySell = (char)buffer[4], // 5th byte = buy/sell
            Sequence = BitConverter.ToUInt32(new byte[] { buffer[8], buffer[7], buffer[6], buffer[5] }, 0), // Bytes 5-8 = sequence
            Quantity = BitConverter.ToUInt32(new byte[] { buffer[12], buffer[11], buffer[10], buffer[9] }, 0), // Bytes 9-12 = quantity
            Price = BitConverter.ToUInt32(new byte[] { buffer[16], buffer[15], buffer[14], buffer[13] }, 0) // Bytes 13-16 = price
        };
    }

    // Find and request missing packets
    static List<Packet> RequestMissingPackets(List<Packet> packets)
    {
        // Find missing sequence numbers
        var sequenceNumbers = new HashSet<uint>(packets.Select(p => p.Sequence));
        uint maxSequence = packets.Count > 0 ? packets.Max(p => p.Sequence) : 0;
        var missingSequences = new List<uint>();
        for (uint i = 1; i <= maxSequence; i++)
        {
            if (!sequenceNumbers.Contains(i))
            {
                missingSequences.Add(i);
            }
        }

        if (missingSequences.Count == 0)
        {
            Console.WriteLine("No packets are missing!");
            return packets;
        }

        Console.WriteLine($"Missing packet sequences: {string.Join(", ", missingSequences)}");

        // Ask server for each missing packet
        foreach (uint seq in missingSequences)
        {
            try
            {
                using var client = new TcpClient(Server, Port);
                using var stream = client.GetStream();

                // Send Call Type 2 (byte 2 + 4-byte sequence number)
                byte[] request = new byte[5];
                request[0] = 2; // Call Type 2
                byte[] seqBytes = BitConverter.GetBytes(seq);
                Array.Reverse(seqBytes); // Convert to big-endian
                Array.Copy(seqBytes, 0, request, 1, 4);
                stream.Write(request, 0, request.Length);
                stream.Flush();

                // Read the response (should be a 17-byte packet)
                byte[] buffer = new byte[17];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 17)
                {
                    Packet packet = ParsePacket(buffer);
                    if (packet.Sequence == seq)
                    {
                        packets.Add(packet);
                        Console.WriteLine($"Got missing packet: Seq={packet.Sequence}");
                    }
                    else
                    {
                        Console.WriteLine($"Wrong packet received for seq {seq}");
                    }
                }
                else
                {
                    Console.WriteLine($"Bad response for seq {seq}: {bytesRead} bytes");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting seq {seq}: {ex.Message}");
            }
        }

        return packets;
    }

    // Save packets to a JSON file
    static void SaveToJson(List<Packet> packets)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(packets, options);
        File.WriteAllText(OutputFile, json);
    }
}
