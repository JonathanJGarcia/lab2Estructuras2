using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.Xml;

class Program
{
    private static BTree bTree = new BTree(9);
    private static Dictionary<string, string> companyCodes = new Dictionary<string, string>();
    private static Dictionary<string, string> reverseCompanyCodes = new Dictionary<string, string>();

    static void Main()
    {
        LoadCommandsFromFile(@"D:\visual studi\2do semestre 2024\Lab 2 estructuras\input.csv");
        UserSearch();
    }

    private static void LoadCommandsFromFile(string path)
    {
        Huffman huffman = new Huffman();

        using (StreamReader sr = new StreamReader(path))
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                string decodedLine = huffman.Decode(huffman.Encode(line));

                JObject jsonObject = JObject.Parse(decodedLine.Substring(decodedLine.IndexOf("{")));

                if (line.StartsWith("INSERT;"))
                {
                    string json = line.Substring(7);
                    Record user = JsonConvert.DeserializeObject<Record>(json);

                    // Codificar DPI
                    user.DPI = huffman.Encode(user.DPI);

                    // Codificar compañías usando diccionario
                    for (int i = 0; i < user.Companies.Count; i++)
                    {
                        string company = user.Companies[i];
                        if (!companyCodes.ContainsKey(company))
                        {
                            string encodedCompany = huffman.Encode(company);
                            companyCodes[company] = encodedCompany;
                            reverseCompanyCodes[encodedCompany] = company;
                        }
                        user.Companies[i] = companyCodes[company];
                    }

                    bTree.Insert(user);
                }
                else if (line.StartsWith("PATCH;"))
                {
                    string json = line.Substring(6);
                    Record newUser = JsonConvert.DeserializeObject<Record>(json);
                    string encodedDpi = huffman.Encode(newUser.DPI);

                    Record existingUser = bTree.SearchByDpi(encodedDpi);
                    if (existingUser != null)
                    {
                        existingUser.DPI = encodedDpi;
                        existingUser.DateBirth = newUser.DateBirth;
                        existingUser.Address = newUser.Address;

                        List<string> encodedCompanies = newUser.Companies
                            .Select(company => companyCodes.ContainsKey(company) ? companyCodes[company] : huffman.Encode(company))
                            .ToList();

                        existingUser.Companies = encodedCompanies;
                    }
                }
                else if (line.StartsWith("DELETE;"))
                {
                    string json = line.Substring(7);
                    Record user = JsonConvert.DeserializeObject<Record>(json);
                    string encodedDpi = huffman.Encode(user.DPI);

                    bTree.Delete(encodedDpi);
                }
            }
        }
    }

    private static void UserSearch()
    {
        Huffman encoder = new Huffman();

        Console.WriteLine("Ingresa un DPI para buscar o escribe 'exit' para salir: ");
        string dpi;
        while ((dpi = Console.ReadLine()) != "exit")
        {
            string encodedDpi = encoder.Encode(dpi);

            // Enseñar el proceso de codificación del DPI
            Console.WriteLine($"Proceso de codificación del DPI '{dpi}': {encodedDpi}");

            Record user = bTree.SearchByDpi(encodedDpi);
            if (user != null)
            {
                // decodificación del DPI
                Console.WriteLine($"Proceso de decodificación del DPI '{user.DPI}':");
                string decodedDpi = encoder.Decode(user.DPI);
                Console.WriteLine($"Resultado final de la decodificación del DPI: {decodedDpi}");

                // Proceso de decodificación de las empresas
                List<string> decodedCompanies = new List<string>();
                foreach (var company in user.Companies)
                {
                    Console.WriteLine($"Proceso de decodificación de la empresa codificada '{company}':");
                    string decodedCompany = reverseCompanyCodes[company];
                    Console.WriteLine($"Resultado final de la decodificación de la empresa: {decodedCompany}");
                    decodedCompanies.Add(decodedCompany);
                }

                Record displayUser = new Record
                {
                    Name = user.Name,
                    DPI = decodedDpi,
                    DateBirth = user.DateBirth,
                    Address = user.Address,
                    Companies = decodedCompanies
                };

                string userJson = JsonConvert.SerializeObject(displayUser, Newtonsoft.Json.Formatting.Indented);
                Console.WriteLine($"Registro encontrado:\n{userJson}");
            }
            else
            {
                Console.WriteLine($"Ningún registro encontrado con el DPI {dpi}");
            }
            Console.WriteLine("Ingresa otro DPI para buscar o escribe 'exit' para salir: ");
        }
    }


}


public class Record
{
    public string Name { get; set; }
    public string DPI { get; set; }
    public string DateBirth { get; set; }
    public string Address { get; set; }
    public List<string> Companies { get; set; }

    public Record()
    {
        Companies = new List<string>();
    }
}

public class BTreeNode
{
    public Record[] Records { get; set; }
    public int Degree { get; private set; }
    public BTreeNode[] Children { get; set; }
    public int RecordCount { get; set; }
    public bool IsLeaf { get; set; }

    public BTreeNode(int degree, bool isLeaf)
    {
        Degree = degree;
        IsLeaf = isLeaf;
        Records = new Record[2 * degree - 1];
        Children = new BTreeNode[2 * degree];
        RecordCount = 0;
    }
}

public class BTree
{
    private BTreeNode root;
    private int degree;

    public BTree(int degree)
    {
        root = null;
        this.degree = degree;
    }

    public Record SearchByDpi(string encodedDpi)
    {
        return SearchByDpi(root, encodedDpi);
    }

    private Record SearchByDpi(BTreeNode node, string encodedDpi)
    {
        int idx = 0;

        while (idx < node.RecordCount && encodedDpi.CompareTo(node.Records[idx].DPI) > 0)
            idx++;

        if (idx < node.RecordCount && node.Records[idx].DPI == encodedDpi)
            return node.Records[idx];

        if (node.IsLeaf)
            return null;

        return SearchByDpi(node.Children[idx], encodedDpi);
    }

    public void Insert(Record user)
    {
        if (root == null)
        {
            root = new BTreeNode(degree, true);
            root.Records[0] = user;
            root.RecordCount = 1;
        }
        else
        {
            if (root.RecordCount == 2 * degree - 1)
            {
                BTreeNode newNode = new BTreeNode(degree, false);
                newNode.Children[0] = root;
                SplitChild(newNode, 0, root);

                int i = 0;
                if (newNode.Records[0].DPI.CompareTo(user.DPI) < 0)
                    i++;
                InsertNonFull(newNode.Children[i], user);

                root = newNode;
            }
            else
            {
                InsertNonFull(root, user);
            }
        }
    }

    public void Delete(string dpi)
    {
        if (root == null)
        {
            Console.WriteLine("El árbol está vacío");
            return;
        }

        DeleteRecursive(root, dpi);

        if (root.RecordCount == 0)
        {
            if (root.IsLeaf)
                root = null;
            else
                root = root.Children[0];
        }
    }

    private void InsertNonFull(BTreeNode node, Record record)
    {
        int i = node.RecordCount - 1;

        if (node.IsLeaf)
        {
            while (i >= 0 && record.DPI.CompareTo(node.Records[i].DPI) < 0)
            {
                node.Records[i + 1] = node.Records[i];
                i--;
            }

            node.Records[i + 1] = record;
            node.RecordCount += 1;
        }
        else
        {
            while (i >= 0 && record.DPI.CompareTo(node.Records[i].DPI) < 0)
                i--;

            i++;
            BTreeNode child = node.Children[i];

            if (child.RecordCount == 2 * degree - 1)
            {
                SplitChild(node, i, child);

                if (record.DPI.CompareTo(node.Records[i].DPI) > 0)
                    i++;
            }

            InsertNonFull(node.Children[i], record);
        }
    }

    private void SplitChild(BTreeNode parentNode, int i, BTreeNode nodeToSplit)
    {
        BTreeNode newNode = new BTreeNode(nodeToSplit.Degree, nodeToSplit.IsLeaf);
        newNode.RecordCount = degree - 1;

        for (int j = 0; j < degree - 1; j++)
            newNode.Records[j] = nodeToSplit.Records[j + degree];

        if (!nodeToSplit.IsLeaf)
        {
            for (int j = 0; j < degree; j++)
                newNode.Children[j] = nodeToSplit.Children[j + degree];
        }

        nodeToSplit.RecordCount = degree - 1;

        for (int j = parentNode.RecordCount; j >= i + 1; j--)
            parentNode.Children[j + 1] = parentNode.Children[j];

        parentNode.Children[i + 1] = newNode;

        for (int j = parentNode.RecordCount - 1; j >= i; j--)
            parentNode.Records[j + 1] = parentNode.Records[j];

        parentNode.Records[i] = nodeToSplit.Records[degree - 1];
        parentNode.RecordCount += 1;
    }

    private void DeleteRecursive(BTreeNode node, string dpi)
    {
        int idx = 0;
        while (idx < node.RecordCount && dpi.CompareTo(node.Records[idx].DPI) > 0)
            idx++;

        if (idx < node.RecordCount && node.Records[idx].DPI == dpi)
        {
            if (node.IsLeaf)
                RemoveFromLeaf(node, idx);
            else
                RemoveFromNonLeaf(node, idx);
        }
        else
        {
            if (node.IsLeaf)
            {
                Console.WriteLine("El DPI {0} no existe en el árbol", dpi);
                return;
            }

            bool flag = (idx == node.RecordCount);

            if (node.Children[idx].RecordCount < degree)
                Fill(node, idx);

            if (flag && idx > node.RecordCount)
                DeleteRecursive(node.Children[idx - 1], dpi);
            else
                DeleteRecursive(node.Children[idx], dpi);
        }
    }

    private void RemoveFromLeaf(BTreeNode node, int idx)
    {
        for (int i = idx + 1; i < node.RecordCount; i++)
            node.Records[i - 1] = node.Records[i];

        node.RecordCount--;
    }

    private void RemoveFromNonLeaf(BTreeNode node, int idx)
    {
        string dpi = node.Records[idx].DPI;

        if (node.Children[idx].RecordCount >= degree)
        {
            Record predecessor = GetPredecessor(node, idx);
            node.Records[idx] = predecessor;
            DeleteRecursive(node.Children[idx], predecessor.DPI);
        }
        else if (node.Children[idx + 1].RecordCount >= degree)
        {
            Record successor = GetSuccessor(node, idx);
            node.Records[idx] = successor;
            DeleteRecursive(node.Children[idx + 1], successor.DPI);
        }
        else
        {
            Merge(node, idx);
            DeleteRecursive(node.Children[idx], dpi);
        }
    }

    private Record GetPredecessor(BTreeNode node, int idx)
    {
        BTreeNode current = node.Children[idx];
        while (!current.IsLeaf)
            current = current.Children[current.RecordCount];

        return current.Records[current.RecordCount - 1];
    }

    private Record GetSuccessor(BTreeNode node, int idx)
    {
        BTreeNode current = node.Children[idx + 1];
        while (!current.IsLeaf)
            current = current.Children[0];

        return current.Records[0];
    }

    private void Fill(BTreeNode node, int idx)
    {
        if (idx != 0 && node.Children[idx - 1].RecordCount >= degree)
            BorrowFromPrev(node, idx);
        else if (idx != node.RecordCount && node.Children[idx + 1].RecordCount >= degree)
            BorrowFromNext(node, idx);
        else
        {
            if (idx != node.RecordCount)
                Merge(node, idx);
            else
                Merge(node, idx - 1);
        }
    }

    private void BorrowFromPrev(BTreeNode node, int idx)
    {
        BTreeNode child = node.Children[idx];
        BTreeNode sibling = node.Children[idx - 1];

        for (int i = child.RecordCount - 1; i >= 0; i--)
            child.Records[i + 1] = child.Records[i];

        if (!child.IsLeaf)
        {
            for (int i = child.RecordCount; i >= 0; i--)
                child.Children[i + 1] = child.Children[i];
        }

        child.Records[0] = node.Records[idx - 1];

        if (!node.IsLeaf)
            child.Children[0] = sibling.Children[sibling.RecordCount];

        node.Records[idx - 1] = sibling.Records[sibling.RecordCount - 1];

        child.RecordCount += 1;
        sibling.RecordCount -= 1;
    }

    private void BorrowFromNext(BTreeNode node, int idx)
    {
        BTreeNode child = node.Children[idx];
        BTreeNode sibling = node.Children[idx + 1];

        child.Records[child.RecordCount] = node.Records[idx];

        if (!child.IsLeaf)
            child.Children[child.RecordCount + 1] = sibling.Children[0];

        node.Records[idx] = sibling.Records[0];

        for (int i = 1; i < sibling.RecordCount; i++)
            sibling.Records[i - 1] = sibling.Records[i];

        if (!sibling.IsLeaf)
        {
            for (int i = 1; i <= sibling.RecordCount; i++)
                sibling.Children[i - 1] = sibling.Children[i];
        }

        child.RecordCount += 1;
        sibling.RecordCount -= 1;
    }

    private void Merge(BTreeNode node, int idx)
    {
        BTreeNode child = node.Children[idx];
        BTreeNode sibling = node.Children[idx + 1];

        child.Records[degree - 1] = node.Records[idx];

        for (int i = 0; i < sibling.RecordCount; i++)
            child.Records[i + degree] = sibling.Records[i];

        if (!child.IsLeaf)
        {
            for (int i = 0; i <= sibling.RecordCount; i++)
                child.Children[i + degree] = sibling.Children[i];
        }

        for (int i = idx + 1; i < node.RecordCount; i++)
            node.Records[i - 1] = node.Records[i];

        for (int i = idx + 2; i <= node.RecordCount; i++)
            node.Children[i - 1] = node.Children[i];

        child.RecordCount += sibling.RecordCount + 1;
        node.RecordCount--;
    }
}


public class HuffmanNode
{
    public char Symbol { get; set; }
    public int Frequency { get; set; }
    public HuffmanNode Left { get; set; }
    public HuffmanNode Right { get; set; }

    public bool IsLeaf => Left == null && Right == null;
}


public class Huffman
{
    private Dictionary<char, string> _huffmanCodes;
    private HuffmanNode _root;

    public Huffman()
    {
        _huffmanCodes = new Dictionary<char, string>();
    }

    // Función para construir el árbol de Huffman 
    private HuffmanNode BuildHuffmanTree(Dictionary<char, int> frequencies)
    {
        var priorityQueue = new List<HuffmanNode>();

        foreach (var symbol in frequencies)
        {
            priorityQueue.Add(new HuffmanNode { Symbol = symbol.Key, Frequency = symbol.Value });
        }

        // Ordenar la lista por la frecuencia
        priorityQueue = priorityQueue.OrderBy(node => node.Frequency).ToList();

        while (priorityQueue.Count > 1)
        {
            var left = priorityQueue[0];
            var right = priorityQueue[1];

            
            priorityQueue.RemoveAt(0);
            priorityQueue.RemoveAt(0);

            var parentNode = new HuffmanNode
            {
                Symbol = '\0',
                Frequency = left.Frequency + right.Frequency,
                Left = left,
                Right = right
            };

            priorityQueue.Add(parentNode);

            
            priorityQueue = priorityQueue.OrderBy(node => node.Frequency).ToList();
        }

        return priorityQueue.First();
    }


    // Generar los códigos de Huffman
    private void GenerateHuffmanCodes(HuffmanNode node, string currentCode)
    {
        if (node == null) return;

        if (node.IsLeaf)
        {
            _huffmanCodes[node.Symbol] = currentCode;
        }

        GenerateHuffmanCodes(node.Left, currentCode + "0");
        GenerateHuffmanCodes(node.Right, currentCode + "1");
    }

    // Codificación 
    public string Encode(string input)
    {
        var frequencies = new Dictionary<char, int>();

        // Calcular la frecuencia de cada símbolo en la entrada
        foreach (char c in input)
        {
            if (!frequencies.ContainsKey(c))
                frequencies[c] = 0;
            frequencies[c]++;
        }

        // Construir el árbol de Huffman
        _root = BuildHuffmanTree(frequencies);

        // Generar los códigos de Huffman para cada símbolo
        GenerateHuffmanCodes(_root, "");

        // Codificar la cadena de entrada
        StringBuilder encodedString = new StringBuilder();
        foreach (char c in input)
        {
            encodedString.Append(_huffmanCodes[c]);
        }

        return encodedString.ToString();
    }

    // Decodificación
    public string Decode(string encodedInput, bool showProcess = false)
    {
        StringBuilder decodedString = new StringBuilder();
        HuffmanNode currentNode = _root;

        if (showProcess)
        {
            Console.WriteLine("Decodificando el DPI:");
        }

        foreach (char bit in encodedInput)
        {
            if (showProcess)
            {
                Console.WriteLine($"Bit leído: {bit}, moviendo en el árbol.");
            }

            if (bit == '0')
            {
                currentNode = currentNode.Left;
            }
            else
            {
                currentNode = currentNode.Right;
            }

            if (currentNode.IsLeaf)
            {
                decodedString.Append(currentNode.Symbol);

                if (showProcess)
                {
                    Console.WriteLine($"Símbolo decodificado: {currentNode.Symbol}");
                }

                currentNode = _root; 
            }
        }

        return decodedString.ToString();
    }
}

