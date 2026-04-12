// Test the escaping logic
string sshKey = @"/home/user/.ssh/id_rsa";
string escaped = sshKey.Replace("'", "'\''");
System.Console.WriteLine($"Original: {sshKey}");
System.Console.WriteLine($"Escaped: {escaped}");

// Test with a malicious key
sshKey = @"/tmp/id_rsa' && cat /etc/passwd #";
escaped = sshKey.Replace("'", "'\''");
System.Console.WriteLine($"Malicious: {sshKey}");
System.Console.WriteLine($"Escaped: {escaped}");

// Test with backticks
sshKey = @"/tmp/id_rsa`whoami`";
escaped = sshKey.Replace("'", "'\''");
System.Console.WriteLine($"Backticks: {sshKey}");
System.Console.WriteLine($"Escaped: {escaped}");

// Test with $()
sshKey = @"/tmp/id_rsa$(whoami)";
escaped = sshKey.Replace("'", "'\''");
System.Console.WriteLine($"Command sub: {sshKey}");
System.Console.WriteLine($"Escaped: {escaped}");
