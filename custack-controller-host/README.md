# custack-controller-host

custack-roboリポジトリでのUnityから受け取ったコントローラ情報を
ロボットに送信するためのホスト用プログラム

<!-- TODO: システム画像をここに張る -->
<!-- ![]() -->

## データフォーマット

X, Y, Z: ASCIIコード文字列（5文字）、下二桁は小数点

<table>
  <thead>
    <tr>
      <th>0</th>
      <th>1</th>
      <th rowpans=5>2..6</th>
      <th>7</th>
      <th rowpans=5>8..12</th>
      <th>13</th>
      <th rowpans=5>14..18</th>
      <th>19</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>STX(0x02)</td>
      <td>+ or -</td>
      <td rowpans=5>12345</td>
      <td>+ or -</td>
      <td rowpans=5>12345</td>
      <td>+ or -</td>
      <td rowpans=5>12345</td>
      <td>ETX(0x03)</td>
    </tr>
    <tr>
      <td>開始</td>
      <td>符号</td>
      <td rowpans=5>X</td>
      <td>符号</td>
      <td rowpans=5>Y</td>
      <td>符号</td>
      <td rowpans=5>Z</td>
      <td>終了</td>
    </tr>
  </tbody>
</table>
