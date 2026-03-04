namespace DeepDiff.Models;

public enum CompareMode { Folder, Text, Image, Hex, Clipboard }

public enum DiffStatus { Same, Different, LeftOnly, RightOnly }

public enum LineStatus { Same, Changed, LeftOnly, RightOnly }
