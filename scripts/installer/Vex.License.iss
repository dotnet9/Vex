// Generated from the UTF-8 LICENSE file (same rules as the changelog embed: apostrophes
// doubled, every non-ASCII char as a #NNNN escape outside quotes, all tokens joined with
// explicit '+', line breaks as bare #10, statement tails carry #10). Why embedded: on CI's
// Inno Setup 6.7.1 runtime, LoadStringFromFile into AnsiString + assignment widens via CP1252
// (the license credits a Chinese name and rendered as mojibake). Regenerate after editing
// LICENSE; the installer contract asserts the English body so drift is caught.
function VLicenseEmbedded: String;
begin
  Result:='MIT License'+#10+#10+'Copyright (c) 2026 '+#30721+#22346+' CodeWF'+#10+#10+'Permission is hereby granted, free of charge, to any person obtaining a copy'+#10+'of this software and associated documentation files (the "Software"), to deal'+#10+'in the Software without restriction, including without limitation the rights'+#10+'to use, copy, modify, merge, publish, distribute, sublicense, and/or sell'+#10+'copies of the Software, and to permit persons to whom the Software is'+#10+'furnished to do so, subject to the following conditions:'+#10+#10+'The above copyright notice and this permission notice shall be included in all'+#10+'copies or substantial portions of the Software.'+#10+#10+'THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR'+#10+'IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,'+#10+'FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE'+#10+'AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER'+#10+'LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,'+#10+'OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE'+#10+'SOFTWARE.'+#10;
end;
