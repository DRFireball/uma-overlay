namespace UmaCompanion.Core.Reading;

/// <summary>A rectangle in captured-frame pixel coordinates (top-left origin).</summary>
public sealed record OcrRect(double X, double Y, double Width, double Height);

/// <summary>One OCR'd text line with its position in the captured frame.</summary>
public sealed record OcrLine(string Text, OcrRect Bounds);
