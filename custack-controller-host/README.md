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
      <th rowpans=4>2..5</th>
      <th rowpans=4>6..9</th>
      <th rowpans=4>10..13</th>
      <th>14</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>SOF(0xAA)</td>
      <td>length</td>
      <td rowpans=4>float 32bit</td>
      <td rowpans=4>float 32bit</td>
      <td rowpans=4>float 32bit</td>
      <td>checksum</td>
    </tr>
    <tr>
      <td>開始</td>
      <td>データ長</td>
      <td rawpans=4>X</td>
      <td rawpans=4>Y</td>
      <td rawpans=4>W</td>
      <td>XORチェックサム</td>
    </tr>
  </tbody>
</table>
