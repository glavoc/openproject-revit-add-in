﻿using System;
using System.Diagnostics;
using System.IO;

namespace OpenProjectNavisworks.Model;

public class CodeListener : TraceListener
{
  public override void Write(string message)
  {
    WriteMessage(message);
  }

  public override void WriteLine(string message)
  {
    WriteMessage(message);
  }

  void WriteMessage(string message)
  {
    using (StreamWriter st = new StreamWriter(DefaultSetting.PathLogFile, true))
    {
      string join = string.Join(": ", $"{DateTime.Now}", message);
      st.WriteLine(join);
      st.Close();
    }
  }
}