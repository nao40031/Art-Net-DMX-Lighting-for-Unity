# Shutter / Strobe

## 日本語

シャッター／ストロボは、光量を連続的に調整するディマーとは別に、光路を時間的に開閉する機能です。`Open` は常時点灯、`Closed` は消灯、`Regular Strobe` は一定周期の点滅、`Pulse Open / Close` は明暗を滑らかに変化させ、`Random` 系は周期内の発光位置を再現可能な乱数で変化させます。フレーミングシャッター（ビームの輪郭を切るブレード）とは別機能です。

Fixture Profile の Strobe 属性・Value ロールに Range を追加し、Range Type と DMX 範囲を設定します。速度を持つ Range では Mapping Context を `Shutter Frequency` にし、DMX 最小・最大側の周波数、Duty Cycle、必要に応じて Flash Duration を指定します。メーカー固有の DMX 値は Range データに閉じ込められるため、実行コードは機種に依存しません。

既定の `Full Fixture Rate` は Fixture Profile に設定した速度を上限なしで再生します。`Safe Maximum 3 Hz` を明示的に選ぶと点滅を最大 3 Hz に制限できます。`Disabled` は Open / Closed のみを反映し、時間変化する点滅を停止します。編集モードでも時間変化する点滅は行いません。

シャッターゲートはディマー値を変更せず、最終出力へ乗算されます。そのため、通常の Light、レンズ発光、Pseudo Beam、VLB、およびプリズム補助光が同じタイミングで開閉します。

## English

Shutter/strobe is a time-domain gate on the light path, separate from the continuously variable dimmer. `Open` stays lit, `Closed` blacks out, `Regular Strobe` flashes at a fixed rate, `Pulse Open / Close` ramps the gate, and random modes vary flash placement using a reproducible seed. This is separate from framing shutters, whose blades shape the beam outline.

Add ranges to the Strobe attribute's Value role in a Fixture Profile, then define each range's DMX interval and Range Type. For variable-speed ranges, use the `Shutter Frequency` mapping context and set the frequencies at the DMX minimum and maximum, the duty cycle, and optionally a fixed flash duration. Manufacturer-specific DMX values stay in profile data, so runtime behavior remains fixture-independent.

The default `Full Fixture Rate` plays the speed configured in the Fixture Profile without a preview cap. Explicitly selecting `Safe Maximum 3 Hz` caps flashing at 3 Hz. `Disabled` still applies Open and Closed states but suppresses animated flashing. Animated flashing is also suppressed outside Play Mode.

The shutter gate does not overwrite the dimmer value. It multiplies the final output so the main Light, lens emission, Pseudo Beam, VLB, and prism auxiliary lights open and close coherently.
