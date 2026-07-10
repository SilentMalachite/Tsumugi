# Phase 3 請求項目マッピング

この文書は機械可読JSONの人間向け索引である。正本は `src/Tsumugi.Infrastructure.Csv/Specifications/*.json` と `docs/spec-data/phase3/*.json` であり、項目追加・削除・状態変更はJSONと完全性テストを先に更新する。

## 集計

| 対象 | field数 | existing | missing | explicitInput | generated |
| --- | ---: | ---: | ---: | ---: | ---: |
| CSV（共通編19 + 事業所編424） | 443 | 28 | 25 | 10 | 380 |
| 3帳票 | 113 | 21 | 20 | 4 | 68 |

帳票inventoryはCSVから生成していない。公式様式・記載例から独立して、実績記録票41、請求書24、請求明細書48を固定した。

## CSV全項目

| fieldId | recordId | position | 公式項目名 | requiredWhen | status | 入力源・生成規則 | source |
| --- | --- | ---: | --- | --- | --- | --- | --- |
| common:outer:control:001 | common:outer:control | 1 | レコード種別 | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:control:002 | common:outer:control | 2 | レコード番号（連番） | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:control:003 | common:outer:control | 3 | ボリューム通番 | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:control:004 | common:outer:control | 4 | レコード件数 | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:control:005 | common:outer:control | 5 | データ種別 | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:control:006 | common:outer:control | 6 | 市町村番号 | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:control:007 | common:outer:control | 7 | 事業所番号 | always | existing | Office.OfficeNumber | common-r7-10 p.5 |
| common:outer:control:008 | common:outer:control | 8 | 都道府県番号 | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:control:009 | common:outer:control | 9 | 媒体区分 | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:control:010 | common:outer:control | 10 | 処理対象年月 | always | explicitInput | ProcessingMonth | common-r7-10 p.5 |
| common:outer:control:011 | common:outer:control | 11 | 予備 | must be empty | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:control:012 | common:outer:control | 12 | ブランク | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:data:001 | common:outer:data | 1 | レコード種別 | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:data:002 | common:outer:data | 2 | レコード番号（連番） | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:data:003 | common:outer:data | 3 | データ | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:data:004 | common:outer:data | 4 | ブランク | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:end:001 | common:outer:end | 1 | レコード種別 | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:end:002 | common:outer:end | 2 | レコード番号（連番） | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| common:outer:end:003 | common:outer:end | 3 | ブランク | always | generated | derive the outer record value from the selected provider records and common R7-10 framing rules | common-r7-10 p.5 |
| provider:J111:01:001 | provider:J111:01 | 1 | 交換情報識別番号 | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.17 |
| provider:J111:01:002 | provider:J111:01 | 2 | レコード種別コード | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.17 |
| provider:J111:01:003 | provider:J111:01 | 3 | サービス提供年月 | always | explicitInput | ServiceProvisionMonth | provider-r7-10 p.17 |
| provider:J111:01:004 | provider:J111:01 | 4 | 市町村番号 | always | missing | Certificate.MunicipalityNumber / CertificateView / migration | provider-r7-10 p.17 |
| provider:J111:01:005 | provider:J111:01 | 5 | 事業所番号 | always | existing | Office.OfficeNumber | provider-r7-10 p.17 |
| provider:J111:01:006 | provider:J111:01 | 6 | 請求金額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:007 | provider:J111:01 | 7 | 小計 介護給付費等・特例介護給付費等 件数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:008 | provider:J111:01 | 8 | 小計 介護給付費等・特例介護給付費等 単位数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:009 | provider:J111:01 | 9 | 小計 介護給付費等・特例介護給付費等 費用合計 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:010 | provider:J111:01 | 10 | 小計 介護給付費等・特例介護給付費等 給付費請求額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:011 | provider:J111:01 | 11 | 小計 介護給付費等・特例介護給付費等 特別対策費請求額 | when the official J111 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:012 | provider:J111:01 | 12 | 小計 介護給付費等・特例介護給付費等 利用者負担額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:013 | provider:J111:01 | 13 | 小計 介護給付費等・特例介護給付費等 自治体助成額 | when the official J111 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:014 | provider:J111:01 | 14 | 小計 特定障害者特別給付費・高額障害福祉サービス費 件数 | when the official J111 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:015 | provider:J111:01 | 15 | 小計 特定障害者特別給付費・高額障害福祉サービス費 費用合計 | when the official J111 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:016 | provider:J111:01 | 16 | 小計 特定障害者特別給付費・高額障害福祉サービス費 給付費請求額 | when the official J111 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:017 | provider:J111:01 | 17 | 合計 件数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:018 | provider:J111:01 | 18 | 合計 単位数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:019 | provider:J111:01 | 19 | 合計 費用合計 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:020 | provider:J111:01 | 20 | 合計 給付費請求額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:021 | provider:J111:01 | 21 | 合計 特別対策費請求額 | when the official J111 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:022 | provider:J111:01 | 22 | 合計 利用者負担額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:01:023 | provider:J111:01 | 23 | 合計 自治体助成額 | when the official J111 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.17 |
| provider:J111:02:001 | provider:J111:02 | 1 | 交換情報識別番号 | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.18 |
| provider:J111:02:002 | provider:J111:02 | 2 | レコード種別コード | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.18 |
| provider:J111:02:003 | provider:J111:02 | 3 | サービス提供年月 | always | explicitInput | ServiceProvisionMonth | provider-r7-10 p.18 |
| provider:J111:02:004 | provider:J111:02 | 4 | 市町村番号 | always | missing | Certificate.MunicipalityNumber / CertificateView / migration | provider-r7-10 p.18 |
| provider:J111:02:005 | provider:J111:02 | 5 | 事業所番号 | always | existing | Office.OfficeNumber | provider-r7-10 p.18 |
| provider:J111:02:006 | provider:J111:02 | 6 | 給付種別 | always | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.18 |
| provider:J111:02:007 | provider:J111:02 | 7 | サービス種類コード | always | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.18 |
| provider:J111:02:008 | provider:J111:02 | 8 | 件数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.18 |
| provider:J111:02:009 | provider:J111:02 | 9 | 単位数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.18 |
| provider:J111:02:010 | provider:J111:02 | 10 | 費用合計 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.18 |
| provider:J111:02:011 | provider:J111:02 | 11 | 給付費請求額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.18 |
| provider:J111:02:012 | provider:J111:02 | 12 | 特別対策費請求額 | when the official J111 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.18 |
| provider:J111:02:013 | provider:J111:02 | 13 | 利用者負担額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.18 |
| provider:J111:02:014 | provider:J111:02 | 14 | 自治体助成額 | when the official J111 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.18 |
| provider:J121:01:001 | provider:J121:01 | 1 | 交換情報識別番号 | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.20 |
| provider:J121:01:002 | provider:J121:01 | 2 | レコード種別コード | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.20 |
| provider:J121:01:003 | provider:J121:01 | 3 | サービス提供年月 | always | explicitInput | ServiceProvisionMonth | provider-r7-10 p.20 |
| provider:J121:01:004 | provider:J121:01 | 4 | 市町村番号 | always | missing | Certificate.MunicipalityNumber / CertificateView / migration | provider-r7-10 p.20 |
| provider:J121:01:005 | provider:J121:01 | 5 | 事業所番号 | always | existing | Office.OfficeNumber | provider-r7-10 p.20 |
| provider:J121:01:006 | provider:J121:01 | 6 | 受給者証番号 | always | existing | Certificate.CertificateNumber | provider-r7-10 p.20 |
| provider:J121:01:007 | provider:J121:01 | 7 | 助成自治体番号 | when the official J121 condition applies | missing | Certificate.SubsidyMunicipalityNumber / CertificateView / migration | provider-r7-10 p.20 |
| provider:J121:01:008 | provider:J121:01 | 8 | 支給決定者氏名カナ | optional | existing | Recipient.KanaName | provider-r7-10 p.20 |
| provider:J121:01:009 | provider:J121:01 | 9 | 支給決定児童氏名カナ | optional | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.20 |
| provider:J121:01:010 | provider:J121:01 | 10 | 地域区分コード | always | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.20 |
| provider:J121:01:011 | provider:J121:01 | 11 | 就労継続支援A型事業者負担 減免措置実施 | always | generated | set 1 (無し) because the supported service is 就労継続支援B型, not A型 | provider-r7-10 p.20 |
| provider:J121:01:012 | provider:J121:01 | 12 | 利用者負担上限月額① | always | existing | Certificate.MonthlyCostCap | provider-r7-10 p.20 |
| provider:J121:01:013 | provider:J121:01 | 13 | 就労継続支援A型減免対象者 | always | generated | set 1 (無し) because the supported service is 就労継続支援B型, not A型 | provider-r7-10 p.20 |
| provider:J121:01:014 | provider:J121:01 | 14 | 障害支援区分コード | when the official J121 condition applies | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.20 |
| provider:J121:01:015 | provider:J121:01 | 15 | 上限額管理事業所 指定事業所番号 | when the official J121 condition applies | missing | Certificate.UpperLimitManagementProviderNumber / CertificateView / migration | provider-r7-10 p.20 |
| provider:J121:01:016 | provider:J121:01 | 16 | 管理結果 | when the official J121 condition applies | missing | ClaimInput.UpperLimitManagementResult / ClaimInputView / migration | provider-r7-10 p.20 |
| provider:J121:01:017 | provider:J121:01 | 17 | 上限額管理事業所 管理結果額 | when the official J121 condition applies | missing | ClaimInput.UpperLimitManagedAmountYen / ClaimInputView / migration | provider-r7-10 p.20 |
| provider:J121:01:018 | provider:J121:01 | 18 | 日 加 中 算 支 欄 援 / 指定事業所番号 | when the official J121 condition applies | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.20 |
| provider:J121:01:019 | provider:J121:01 | 19 | 当該事業所への 通所日数 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:020 | provider:J121:01 | 20 | 請 求 額 集 計 欄 合 計 / 給付単位数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:021 | provider:J121:01 | 21 | 総費用額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:022 | provider:J121:01 | 22 | 上限月額調整（① ②の内少ない数） | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:023 | provider:J121:01 | 23 | 請 求 額 集 計 欄 合 計 / A 型 減 免 / 事業者減免額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:024 | provider:J121:01 | 24 | 減免後利用者 負担額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:025 | provider:J121:01 | 25 | 調整後利用者 負担額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:026 | provider:J121:01 | 26 | 上限額管理後 利用者負担額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:027 | provider:J121:01 | 27 | 決定利用者負担額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:028 | provider:J121:01 | 28 | 請 求 額 / 給付費 | always | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.20 |
| provider:J121:01:029 | provider:J121:01 | 29 | 高額障害福祉 サービス費 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:030 | provider:J121:01 | 30 | 特別対策費 | when the official J121 condition applies | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.20 |
| provider:J121:01:031 | provider:J121:01 | 31 | 自治体助成分 請求額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:032 | provider:J121:01 | 32 | 特 定 障 給 （ 合 害 付 計 者 費 ） 特 別 / 算定日額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:033 | provider:J121:01 | 33 | 日数 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:034 | provider:J121:01 | 34 | 給付費請求額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:01:035 | provider:J121:01 | 35 | 実費算定額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.20 |
| provider:J121:02:001 | provider:J121:02 | 1 | 交換情報識別番号 | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.22 |
| provider:J121:02:002 | provider:J121:02 | 2 | レコード種別コード | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.22 |
| provider:J121:02:003 | provider:J121:02 | 3 | サービス提供年月 | always | explicitInput | ServiceProvisionMonth | provider-r7-10 p.22 |
| provider:J121:02:004 | provider:J121:02 | 4 | 市町村番号 | always | missing | Certificate.MunicipalityNumber / CertificateView / migration | provider-r7-10 p.22 |
| provider:J121:02:005 | provider:J121:02 | 5 | 事業所番号 | always | existing | Office.OfficeNumber | provider-r7-10 p.22 |
| provider:J121:02:006 | provider:J121:02 | 6 | 受給者証番号 | always | existing | Certificate.CertificateNumber | provider-r7-10 p.22 |
| provider:J121:02:007 | provider:J121:02 | 7 | サービス種類コード | always | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.22 |
| provider:J121:02:008 | provider:J121:02 | 8 | サービス開始日等 開始年月日 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.22 |
| provider:J121:02:009 | provider:J121:02 | 9 | 終了年月日 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.22 |
| provider:J121:02:010 | provider:J121:02 | 10 | 利用日数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.22 |
| provider:J121:02:011 | provider:J121:02 | 11 | 入院日数 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.22 |
| provider:J121:02:012 | provider:J121:02 | 12 | 外泊日数 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.22 |
| provider:J121:03:001 | provider:J121:03 | 1 | 交換情報識別番号 | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.31 |
| provider:J121:03:002 | provider:J121:03 | 2 | レコード種別コード | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.31 |
| provider:J121:03:003 | provider:J121:03 | 3 | サービス提供年月 | always | explicitInput | ServiceProvisionMonth | provider-r7-10 p.31 |
| provider:J121:03:004 | provider:J121:03 | 4 | 市町村番号 | always | missing | Certificate.MunicipalityNumber / CertificateView / migration | provider-r7-10 p.31 |
| provider:J121:03:005 | provider:J121:03 | 5 | 事業所番号 | always | existing | Office.OfficeNumber | provider-r7-10 p.31 |
| provider:J121:03:006 | provider:J121:03 | 6 | 受給者証番号 | always | existing | Certificate.CertificateNumber | provider-r7-10 p.31 |
| provider:J121:03:007 | provider:J121:03 | 7 | サービスコード | always | generated | resolve the official code from the effective claim master and calculated claim line | provider-r7-10 p.31 |
| provider:J121:03:008 | provider:J121:03 | 8 | 単位数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.31 |
| provider:J121:03:009 | provider:J121:03 | 9 | 回数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.31 |
| provider:J121:03:010 | provider:J121:03 | 10 | サービス単位数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.31 |
| provider:J121:03:011 | provider:J121:03 | 11 | 摘要 | when the official J121 condition applies | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.31 |
| provider:J121:04:001 | provider:J121:04 | 1 | 交換情報識別番号 | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.32 |
| provider:J121:04:002 | provider:J121:04 | 2 | レコード種別コード | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.32 |
| provider:J121:04:003 | provider:J121:04 | 3 | サービス提供年月 | always | explicitInput | ServiceProvisionMonth | provider-r7-10 p.32 |
| provider:J121:04:004 | provider:J121:04 | 4 | 市町村番号 | always | missing | Certificate.MunicipalityNumber / CertificateView / migration | provider-r7-10 p.32 |
| provider:J121:04:005 | provider:J121:04 | 5 | 事業所番号 | always | existing | Office.OfficeNumber | provider-r7-10 p.32 |
| provider:J121:04:006 | provider:J121:04 | 6 | 受給者証番号 | always | existing | Certificate.CertificateNumber | provider-r7-10 p.32 |
| provider:J121:04:007 | provider:J121:04 | 7 | サービス種類コード | always | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.32 |
| provider:J121:04:008 | provider:J121:04 | 8 | 請 求 額 集 計 欄 / 集計欄分類番号 | always | generated | set 1 because B型 does not use the alternate child-transition unit-price classification | provider-r7-10 p.32 |
| provider:J121:04:009 | provider:J121:04 | 9 | サービス利用日数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:010 | provider:J121:04 | 10 | 給付単位数 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:011 | provider:J121:04 | 11 | 単位数単価 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:012 | provider:J121:04 | 12 | 給付率 | always | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.32 |
| provider:J121:04:013 | provider:J121:04 | 13 | 総費用額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:014 | provider:J121:04 | 14 | １割相当額 （サービス提供年月 が平成24年3月以 前：給付率に基づく 請求額） | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:015 | provider:J121:04 | 15 | 利用者負担額② （サービス提供年月 が平成24年3月以 前：給付率に基づく 利用者負担額②） | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:016 | provider:J121:04 | 16 | 上限月額調整（① ②の内少ない数） | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:017 | provider:J121:04 | 17 | A 型 減免 / 事業者減免 額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:018 | provider:J121:04 | 18 | 請 求 額 集 計 欄 / A 型 減免 / 減免後利用 者負担額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:019 | provider:J121:04 | 19 | 調整後利用者 負担額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:020 | provider:J121:04 | 20 | 上限額管理後 利用者負担額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:021 | provider:J121:04 | 21 | 決定利用者負担額 | always | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:022 | provider:J121:04 | 22 | 請 求 額 / 給付費 | always | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.32 |
| provider:J121:04:023 | provider:J121:04 | 23 | 高額障害福 祉サービス 費 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:024 | provider:J121:04 | 24 | 特別対策費 | when the official J121 condition applies | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.32 |
| provider:J121:04:025 | provider:J121:04 | 25 | 自治体助成分 請求額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:026 | provider:J121:04 | 26 | 特 定 障 害 者 特 別 給 付 費 / 算定日額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:027 | provider:J121:04 | 27 | 日数 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:028 | provider:J121:04 | 28 | 給付費請求額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:029 | provider:J121:04 | 29 | 実費算定額 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:030 | provider:J121:04 | 30 | 利 用 日 数 管 理 票 / 対象期間（開始） | when the official J121 condition applies | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.32 |
| provider:J121:04:031 | provider:J121:04 | 31 | 対象期間（終了） | when the official J121 condition applies | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.32 |
| provider:J121:04:032 | provider:J121:04 | 32 | 当月の利用日数 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:04:033 | provider:J121:04 | 33 | 原則日数の総和 | when the official J121 condition applies | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.32 |
| provider:J121:05:001 | provider:J121:05 | 1 | 交換情報識別番号 | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.38 |
| provider:J121:05:002 | provider:J121:05 | 2 | レコード種別コード | always | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.38 |
| provider:J121:05:003 | provider:J121:05 | 3 | サービス提供年月 | always | explicitInput | ServiceProvisionMonth | provider-r7-10 p.38 |
| provider:J121:05:004 | provider:J121:05 | 4 | 市町村番号 | always | missing | Certificate.MunicipalityNumber / CertificateView / migration | provider-r7-10 p.38 |
| provider:J121:05:005 | provider:J121:05 | 5 | 事業所番号 | always | existing | Office.OfficeNumber | provider-r7-10 p.38 |
| provider:J121:05:006 | provider:J121:05 | 6 | 受給者証番号 | always | existing | Certificate.CertificateNumber | provider-r7-10 p.38 |
| provider:J121:05:007 | provider:J121:05 | 7 | 決定サービスコード | always | generated | resolve the official code from the effective claim master and calculated claim line | provider-r7-10 p.38 |
| provider:J121:05:008 | provider:J121:05 | 8 | 契約支給量 | always | existing | ContractedProvider.ContractedSupplyDays | provider-r7-10 p.38 |
| provider:J121:05:009 | provider:J121:05 | 9 | 契約開始年月日 | always | existing | ContractedProvider.ContractDate | provider-r7-10 p.38 |
| provider:J121:05:010 | provider:J121:05 | 10 | 契約終了年月日 | when the official J121 condition applies | existing | ContractedProvider.TerminationDate | provider-r7-10 p.38 |
| provider:J121:05:011 | provider:J121:05 | 11 | 事業者記入欄番号 | always | missing | ContractedProvider.CertificateEntryNumber / CertificateView / migration | provider-r7-10 p.38 |
| provider:J611:01:001 | provider:J611:01 | 1 | 交換情報識別番号 | always for form 1701 | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.60 |
| provider:J611:01:002 | provider:J611:01 | 2 | レコード種別コード | always for form 1701 | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.60 |
| provider:J611:01:003 | provider:J611:01 | 3 | サービス提供年月 | always for form 1701 | explicitInput | ServiceProvisionMonth | provider-r7-10 p.60 |
| provider:J611:01:004 | provider:J611:01 | 4 | 市町村番号 | always for form 1701 | missing | Certificate.MunicipalityNumber / CertificateView / migration | provider-r7-10 p.60 |
| provider:J611:01:005 | provider:J611:01 | 5 | 事業所番号 | always for form 1701 | existing | Office.OfficeNumber | provider-r7-10 p.60 |
| provider:J611:01:006 | provider:J611:01 | 6 | 受給者証番号 | always for form 1701 | existing | Certificate.CertificateNumber | provider-r7-10 p.60 |
| provider:J611:01:007 | provider:J611:01 | 7 | 様式種別番号 | always for form 1701 | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.60 |
| provider:J611:01:008 | provider:J611:01 | 8 | 補 足 給 付 関 係 情 報 / 補足給付適用の有無 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:009 | provider:J611:01 | 9 | 補足給付額（円／日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:010 | provider:J611:01 | 10 | 食費の単価 朝食（円／日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:011 | provider:J611:01 | 11 | 食費の単価 昼食（円／日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:012 | provider:J611:01 | 12 | 食費の単価 夕食（円／日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:013 | provider:J611:01 | 13 | 食費の単価 一日（円／日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:014 | provider:J611:01 | 14 | 光熱水費の単価 一日（円／日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:015 | provider:J611:01 | 15 | 光熱水費の単価 一月（円／月） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:016 | provider:J611:01 | 16 | 合 計 １ / 内訳 １００％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:017 | provider:J611:01 | 17 | 内訳 ７０％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:018 | provider:J611:01 | 18 | 内訳 重訪 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:019 | provider:J611:01 | 19 | 合 計 １ / 合計 算定時間数計 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:020 | provider:J611:01 | 20 | 合 計 ２ / 内訳 １００％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:021 | provider:J611:01 | 21 | 内訳 ７０％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:022 | provider:J611:01 | 22 | 内訳 重訪 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:023 | provider:J611:01 | 23 | 合計 算定時間数計 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:024 | provider:J611:01 | 24 | 合 計 ３ / 内訳 １００％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:025 | provider:J611:01 | 25 | 内訳 ９０％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:026 | provider:J611:01 | 26 | 合計 算定時間数計 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:027 | provider:J611:01 | 27 | 合 計 ４ / 内訳 １００％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:028 | provider:J611:01 | 28 | 合 計 ４ / 内訳 ９０％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:029 | provider:J611:01 | 29 | 合計 算定時間数計 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:030 | provider:J611:01 | 30 | 合 計 ５ / 内訳 １００％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:031 | provider:J611:01 | 31 | 内訳 ９０％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:032 | provider:J611:01 | 32 | 合計 算定回数計 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:033 | provider:J611:01 | 33 | 提 供 実 績 の 合 計 / 算定 移動介護分 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:034 | provider:J611:01 | 34 | 実績 送迎加算（回） | when form 1701 records the item | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.60 |
| provider:J611:01:035 | provider:J611:01 | 35 | 実績 家庭連携加算（回） （サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:036 | provider:J611:01 | 36 | 実績 家庭連携加算（回） （算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:037 | provider:J611:01 | 37 | 合計 算定日数（日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:038 | provider:J611:01 | 38 | 夜間支援体制加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:039 | provider:J611:01 | 39 | 日中支援加算（回） （サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:040 | provider:J611:01 | 40 | 日中支援加算（回） （算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:041 | provider:J611:01 | 41 | 通所型（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:042 | provider:J611:01 | 42 | 訪問型 １時間未満（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:043 | provider:J611:01 | 43 | 訪問型 １時間以上（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:044 | provider:J611:01 | 44 | 短期滞在加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:045 | provider:J611:01 | 45 | 提 供 実 績 の 合 計 / 食事提供加算（回） | when form 1701 records the item | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.60 |
| provider:J611:01:046 | provider:J611:01 | 46 | 入院・外泊時加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:047 | provider:J611:01 | 47 | 入院時支援特別加算（回） （サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:048 | provider:J611:01 | 48 | 入院時支援特別加算（回） （算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:049 | provider:J611:01 | 49 | 自立生活支援加算（Ⅱ）（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:050 | provider:J611:01 | 50 | 自活訓練加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:051 | provider:J611:01 | 51 | 訪問支援特別加算（回） （サービス提供回数） | when form 1701 records the item | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.60 |
| provider:J611:01:052 | provider:J611:01 | 52 | 訪問支援特別加算（回） （算定回数） | when form 1701 records the item | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.60 |
| provider:J611:01:053 | provider:J611:01 | 53 | 施設外支援 当月（日） | when form 1701 records the item | generated | count effective DailyRecord entries whose OffsiteSupportApplied input is true in ServiceProvisionMonth | provider-r7-10 p.60 |
| provider:J611:01:054 | provider:J611:01 | 54 | 施設外支援 累計（日／１８０日） | when form 1701 records the item | generated | accumulate effective OffsiteSupportApplied days in the official 180-day window | provider-r7-10 p.60 |
| provider:J611:01:055 | provider:J611:01 | 55 | 帰宅時支援加算（回） （サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:056 | provider:J611:01 | 56 | 帰宅時支援加算（回） （算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:057 | provider:J611:01 | 57 | 実 費 算 定 の 合 計 / 朝食（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:058 | provider:J611:01 | 58 | 昼食（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:059 | provider:J611:01 | 59 | 夕食（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:060 | provider:J611:01 | 60 | 光熱水費（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:061 | provider:J611:01 | 61 | 各小計 食事（円） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:062 | provider:J611:01 | 62 | 各小計 光熱水費（円） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:063 | provider:J611:01 | 63 | 実費合計額（円） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:064 | provider:J611:01 | 64 | 入 所 時 特 別 支 援 加 算 / 利用開始日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:065 | provider:J611:01 | 65 | ３０日目（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:066 | provider:J611:01 | 66 | 当月算定日数（日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:067 | provider:J611:01 | 67 | 退 所 時 特 別 支 援 加 算 / 入所中算定日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:068 | provider:J611:01 | 68 | 退所日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:069 | provider:J611:01 | 69 | 退所後算定日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:070 | provider:J611:01 | 70 | 初 期 加 算 / 利用開始日（年月日） | when form 1701 records the item | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.60 |
| provider:J611:01:071 | provider:J611:01 | 71 | ３０日目（年月日） | when form 1701 records the item | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.60 |
| provider:J611:01:072 | provider:J611:01 | 72 | 当月算定日数（日） | when form 1701 records the item | generated | derive the value deterministically from effective contracts, daily records, claim masters, and calculation results | provider-r7-10 p.60 |
| provider:J611:01:073 | provider:J611:01 | 73 | 地 域 移 行 加 算 / 入所中算定日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:074 | provider:J611:01 | 74 | 退所日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:075 | provider:J611:01 | 75 | 退所後算定日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:076 | provider:J611:01 | 76 | 重 度 包 括 / 実績単位数（単位） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:077 | provider:J611:01 | 77 | 実績割合（％） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:078 | provider:J611:01 | 78 | 支給決定量（単位） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:079 | provider:J611:01 | 79 | 重 度 包 括 / 報酬請求額（円） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:080 | provider:J611:01 | 80 | 利用者負担上限月額（円） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:081 | provider:J611:01 | 81 | 利用者負担額（円） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:082 | provider:J611:01 | 82 | 共同生活援助合計日数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:083 | provider:J611:01 | 83 | 短期入所合計日数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:084 | provider:J611:01 | 84 | その他サービス合計時間数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:085 | provider:J611:01 | 85 | 当該月の日数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:086 | provider:J611:01 | 86 | サービス担当者会議開催日 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:087 | provider:J611:01 | 87 | 重 度 訪 問 介 護 （ 様 式 ３ － ２ ） 集 計 欄 / 第１時間帯 早朝 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:088 | provider:J611:01 | 88 | 第１時間帯 日中 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:089 | provider:J611:01 | 89 | 第１時間帯 夜間 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:090 | provider:J611:01 | 90 | 第１時間帯 深夜 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:091 | provider:J611:01 | 91 | 第２時間帯 早朝 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:092 | provider:J611:01 | 92 | 第２時間帯 日中 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:093 | provider:J611:01 | 93 | 第２時間帯 夜間 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:094 | provider:J611:01 | 94 | 第２時間帯 深夜 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:095 | provider:J611:01 | 95 | 第３時間帯 早朝 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:096 | provider:J611:01 | 96 | 第３時間帯 日中 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:097 | provider:J611:01 | 97 | 第３時間帯 夜間 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:098 | provider:J611:01 | 98 | 第３時間帯 深夜 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:099 | provider:J611:01 | 99 | 第４時間帯 早朝 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:100 | provider:J611:01 | 100 | 第４時間帯 日中 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:101 | provider:J611:01 | 101 | 重 度 訪 問 介 護 （ 様 式 ３ － ２ ） 集 計 欄 / 第４時間帯 夜間 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:102 | provider:J611:01 | 102 | 第４時間帯 深夜 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:103 | provider:J611:01 | 103 | 第５時間帯 早朝 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:104 | provider:J611:01 | 104 | 第５時間帯 日中 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:105 | provider:J611:01 | 105 | 第５時間帯 夜間 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:106 | provider:J611:01 | 106 | 第５時間帯 深夜 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:107 | provider:J611:01 | 107 | 第６時間帯 早朝 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:108 | provider:J611:01 | 108 | 第６時間帯 日中 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:109 | provider:J611:01 | 109 | 第６時間帯 夜間 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:110 | provider:J611:01 | 110 | 第６時間帯 深夜 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:111 | provider:J611:01 | 111 | 施設種類 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:112 | provider:J611:01 | 112 | 提 供 実 績 の 合 計 ２ / 緊急時対応加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:113 | provider:J611:01 | 113 | 初回加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:114 | provider:J611:01 | 114 | 福祉専門職員等連携加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:115 | provider:J611:01 | 115 | 行動障害支援連携加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:116 | provider:J611:01 | 116 | 行動障害支援指導連携加算 （回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:117 | provider:J611:01 | 117 | 医療連携体制加算（回） | when form 1701 records the item | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.60 |
| provider:J611:01:118 | provider:J611:01 | 118 | 緊急短期入所受入加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:119 | provider:J611:01 | 119 | 単独型加算(一定の条件を満た す場合）（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:120 | provider:J611:01 | 120 | 重度障害者支援加算(一定の条 件を満たす場合）（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:121 | provider:J611:01 | 121 | 提 供 実 績 の 合 計 ２ / 家族支援加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:122 | provider:J611:01 | 122 | 同行支援（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:123 | provider:J611:01 | 123 | 特別地域加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:124 | provider:J611:01 | 124 | 低所得者利用加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:125 | provider:J611:01 | 125 | 体験利用支援加算（回） | when form 1701 records the item | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.60 |
| provider:J611:01:126 | provider:J611:01 | 126 | 定員超過特例加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:127 | provider:J611:01 | 127 | 通勤訓練加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:128 | provider:J611:01 | 128 | 地域移行加算(回) | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:129 | provider:J611:01 | 129 | 地域移行促進加算(回) | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:130 | provider:J611:01 | 130 | 住居外利用（日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:131 | provider:J611:01 | 131 | 合 計 １ / 内訳 生活援助 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:132 | provider:J611:01 | 132 | 合 計 ２ / 内訳 ９０％ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:133 | provider:J611:01 | 133 | 内訳 生活援助 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:134 | provider:J611:01 | 134 | 合 計 ３ / 内訳 生活援助 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:135 | provider:J611:01 | 135 | 合 計 ４ / 内訳 生活援助 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:136 | provider:J611:01 | 136 | 合 計 ５ / 内訳 生活援助 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:137 | provider:J611:01 | 137 | 重 度 包 括 / 共同生活援助合計単位数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:138 | provider:J611:01 | 138 | 短期入所合計単位数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:139 | provider:J611:01 | 139 | その他サービス合計単位数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:140 | provider:J611:01 | 140 | 移 保 行 育 支 ・ 援 教 加 育 算 等 / 移行日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:141 | provider:J611:01 | 141 | 移行後算定日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:142 | provider:J611:01 | 142 | 通 所 施 加 設 算 移 行 支 援 / 移行日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:143 | provider:J611:01 | 143 | 算定日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:144 | provider:J611:01 | 144 | 提 供 実 績 の 合 計 ３ / 緊急時支援加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:145 | provider:J611:01 | 145 | 支援計画会議実施加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:146 | provider:J611:01 | 146 | 定着支援連携促進加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:147 | provider:J611:01 | 147 | 移動介護緊急時支援加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:148 | provider:J611:01 | 148 | 日常生活支援情報提供加算 （回）（サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:149 | provider:J611:01 | 149 | 日常生活支援情報提供加算 （回）（算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:150 | provider:J611:01 | 150 | 地域居住支援体制強化推進加 算（回）（サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:151 | provider:J611:01 | 151 | 地域居住支援体制強化推進加 算（回）（算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:152 | provider:J611:01 | 152 | 地域協働加算（回） | when form 1701 records the item | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.60 |
| provider:J611:01:153 | provider:J611:01 | 153 | 支援レポート共有日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:154 | provider:J611:01 | 154 | 入院開始日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:155 | provider:J611:01 | 155 | 移行支援住居入居日（年月日） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:156 | provider:J611:01 | 156 | 集中的支援加算 支援開始日（年月日） | when form 1701 records the item | missing | IntensiveSupportEpisode.StartDate / DailyRecordView / migration | provider-r7-10 p.60 |
| provider:J611:01:157 | provider:J611:01 | 157 | 提 供 実 績 の 合 計 ４ / 有資格者支援加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:158 | provider:J611:01 | 158 | 通院支援加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:159 | provider:J611:01 | 159 | 入浴支援加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:160 | provider:J611:01 | 160 | 喀痰吸引等実施加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:161 | provider:J611:01 | 161 | 専門的支援加算（支援実施時） （回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:162 | provider:J611:01 | 162 | 通所自立支援加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:163 | provider:J611:01 | 163 | 子育てサポート加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:164 | provider:J611:01 | 164 | 訪問支援員特別加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:165 | provider:J611:01 | 165 | 多職種連携支援加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:166 | provider:J611:01 | 166 | 強度行動障害児支援加算（支援 実施時）（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:167 | provider:J611:01 | 167 | 集中的支援加算（回） | when form 1701 records the item | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.60 |
| provider:J611:01:168 | provider:J611:01 | 168 | 緊急時受入加算（回） | when form 1701 records the item | generated | derive the official value from the claim context or emit empty when the record condition does not apply | provider-r7-10 p.60 |
| provider:J611:01:169 | provider:J611:01 | 169 | 自立生活支援加算（Ⅰ）（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:170 | provider:J611:01 | 170 | 延長支援加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:171 | provider:J611:01 | 171 | 要支援児童加算（Ⅱ）（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:01:172 | provider:J611:01 | 172 | 自立サポート加算（回） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.60 |
| provider:J611:02:001 | provider:J611:02 | 1 | 交換情報識別番号 | always for form 1701 | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.72 |
| provider:J611:02:002 | provider:J611:02 | 2 | レコード種別コード | always for form 1701 | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.72 |
| provider:J611:02:003 | provider:J611:02 | 3 | サービス提供年月 | always for form 1701 | explicitInput | ServiceProvisionMonth | provider-r7-10 p.72 |
| provider:J611:02:004 | provider:J611:02 | 4 | 市町村番号 | always for form 1701 | missing | Certificate.MunicipalityNumber / CertificateView / migration | provider-r7-10 p.72 |
| provider:J611:02:005 | provider:J611:02 | 5 | 事業所番号 | always for form 1701 | existing | Office.OfficeNumber | provider-r7-10 p.72 |
| provider:J611:02:006 | provider:J611:02 | 6 | 受給者証番号 | always for form 1701 | existing | Certificate.CertificateNumber | provider-r7-10 p.72 |
| provider:J611:02:007 | provider:J611:02 | 7 | 様式種別番号 | always for form 1701 | generated | emit the fixed official code declared by this record specification | provider-r7-10 p.72 |
| provider:J611:02:008 | provider:J611:02 | 8 | 提供通番 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:009 | provider:J611:02 | 9 | 日付 | always for form 1701 | existing | DailyRecord.ServiceDate | provider-r7-10 p.72 |
| provider:J611:02:010 | provider:J611:02 | 10 | サービス提供回数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:011 | provider:J611:02 | 11 | サービス内容 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:012 | provider:J611:02 | 12 | ヘルパー資格 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:013 | provider:J611:02 | 13 | 運転フラグ | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:014 | provider:J611:02 | 14 | 開始時間 | when form 1701 records the item | missing | DailyRecord.ServiceStartTime / DailyRecordView / migration | provider-r7-10 p.72 |
| provider:J611:02:015 | provider:J611:02 | 15 | 終了時間 | when form 1701 records the item | missing | DailyRecord.ServiceEndTime / DailyRecordView / migration | provider-r7-10 p.72 |
| provider:J611:02:016 | provider:J611:02 | 16 | 算定時間数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:017 | provider:J611:02 | 17 | 乗降（回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:018 | provider:J611:02 | 18 | 移動 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:019 | provider:J611:02 | 19 | 派遣人数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:020 | provider:J611:02 | 20 | 前月からの継続サービス | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:021 | provider:J611:02 | 21 | 送迎加算 往 | when form 1701 records the item | existing | DailyRecord.Transport | provider-r7-10 p.72 |
| provider:J611:02:022 | provider:J611:02 | 22 | 送迎加算 復 | when form 1701 records the item | existing | DailyRecord.Transport | provider-r7-10 p.72 |
| provider:J611:02:023 | provider:J611:02 | 23 | 家庭連携加算 （サービス提供時間数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:024 | provider:J611:02 | 24 | 家庭連携加算 （算定時間数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:025 | provider:J611:02 | 25 | 自活訓練加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:026 | provider:J611:02 | 26 | 短期滞在加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:027 | provider:J611:02 | 27 | 訪問支援特別加算 （サービス提供時間数） | when form 1701 records the item | missing | DailyRecord.SpecialVisitSupportMinutes / DailyRecordView / migration | provider-r7-10 p.72 |
| provider:J611:02:028 | provider:J611:02 | 28 | 訪問支援特別加算 （算定時間数） | when form 1701 records the item | missing | DailyRecord.SpecialVisitSupportMinutes / DailyRecordView / migration | provider-r7-10 p.72 |
| provider:J611:02:029 | provider:J611:02 | 29 | 施設外支援 | when form 1701 records the item | missing | DailyRecord.OffsiteSupportApplied / DailyRecordView / migration | provider-r7-10 p.72 |
| provider:J611:02:030 | provider:J611:02 | 30 | 退所時特別支援加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:031 | provider:J611:02 | 31 | 地域移行加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:032 | provider:J611:02 | 32 | 食事提供加算 | when form 1701 records the item | existing | DailyRecord.MealProvided | provider-r7-10 p.72 |
| provider:J611:02:033 | provider:J611:02 | 33 | 入院・外泊時加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:034 | provider:J611:02 | 34 | 提供形態 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:035 | provider:J611:02 | 35 | 備考 | when form 1701 records the item | existing | DailyRecord.Note | provider-r7-10 p.72 |
| provider:J611:02:036 | provider:J611:02 | 36 | サービス提供の状況 | when form 1701 records the item | existing | DailyRecord.Attendance | provider-r7-10 p.72 |
| provider:J611:02:037 | provider:J611:02 | 37 | 夜間支援体制加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:038 | provider:J611:02 | 38 | 入院時支援特別加算 （サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:039 | provider:J611:02 | 39 | 入院時支援特別加算 （算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:040 | provider:J611:02 | 40 | 帰宅時支援加算 （サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:041 | provider:J611:02 | 41 | 帰宅時支援加算 （算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:042 | provider:J611:02 | 42 | 自立生活支援加算（Ⅱ） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:043 | provider:J611:02 | 43 | 日中支援加算 （サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:044 | provider:J611:02 | 44 | 日中支援加算 （算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:045 | provider:J611:02 | 45 | 算定日数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:046 | provider:J611:02 | 46 | 自立訓練 訪問型時間数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:047 | provider:J611:02 | 47 | 実 費 算 定 / 朝食 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:048 | provider:J611:02 | 48 | 昼食 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:049 | provider:J611:02 | 49 | 夕食 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:050 | provider:J611:02 | 50 | 光熱水費 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:051 | provider:J611:02 | 51 | 重 度 包 括 / 適用単価 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:052 | provider:J611:02 | 52 | 基本単位数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:053 | provider:J611:02 | 53 | 加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:054 | provider:J611:02 | 54 | 加算後単位数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:055 | provider:J611:02 | 55 | 単位数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:056 | provider:J611:02 | 56 | 重 度 包 括 / 1日計 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:057 | provider:J611:02 | 57 | 重 度 訪 問 （ 様 式 ３ － ２ ） / １時間（１３時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:058 | provider:J611:02 | 58 | ２時間（１４時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:059 | provider:J611:02 | 59 | ３時間（１５時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:060 | provider:J611:02 | 60 | ４時間（１６時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:061 | provider:J611:02 | 61 | ５時間（１７時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:062 | provider:J611:02 | 62 | ６時間（１８時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:063 | provider:J611:02 | 63 | ７時間（１９時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:064 | provider:J611:02 | 64 | ８時間（２０時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:065 | provider:J611:02 | 65 | ９時間（２１時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:066 | provider:J611:02 | 66 | １０時間（２２時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:067 | provider:J611:02 | 67 | １１時間（２３時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:068 | provider:J611:02 | 68 | １２時間（２４時間） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:069 | provider:J611:02 | 69 | 緊急時対応加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:070 | provider:J611:02 | 70 | 初回加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:071 | provider:J611:02 | 71 | 福祉専門職員等連携加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:072 | provider:J611:02 | 72 | 行動障害支援連携加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:073 | provider:J611:02 | 73 | 行動障害支援指導連携加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:074 | provider:J611:02 | 74 | 医療連携体制加算 | when form 1701 records the item | missing | DailyRecord.MedicalCoordinationType / DailyRecordView / migration | provider-r7-10 p.72 |
| provider:J611:02:075 | provider:J611:02 | 75 | 緊急短期入所受入加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:076 | provider:J611:02 | 76 | 単独型加算(一定の条件を満 たす場合） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:077 | provider:J611:02 | 77 | 重度障害者支援加算(一定 の条件を満たす場合） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:078 | provider:J611:02 | 78 | 家族支援加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:079 | provider:J611:02 | 79 | 利用人数 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:080 | provider:J611:02 | 80 | 同行支援 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:081 | provider:J611:02 | 81 | 特別地域加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:082 | provider:J611:02 | 82 | 低所得者利用加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:083 | provider:J611:02 | 83 | 体験利用支援加算 | when form 1701 records the item | missing | DailyRecord.TrialUseSupportType / DailyRecordView / migration | provider-r7-10 p.72 |
| provider:J611:02:084 | provider:J611:02 | 84 | 定員超過特例加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:085 | provider:J611:02 | 85 | 通勤訓練加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:086 | provider:J611:02 | 86 | 地域移行促進加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:087 | provider:J611:02 | 87 | 住居外利用 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:088 | provider:J611:02 | 88 | 緊急時支援加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:089 | provider:J611:02 | 89 | 支援計画会議実施加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:090 | provider:J611:02 | 90 | 定着支援連携促進加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:091 | provider:J611:02 | 91 | 移動介護緊急時支援加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:092 | provider:J611:02 | 92 | 日常生活支援情報提供加算 （サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:093 | provider:J611:02 | 93 | 日常生活支援情報提供加算 （算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:094 | provider:J611:02 | 94 | 地域居住支援体制強化推進 加算（サービス提供回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:095 | provider:J611:02 | 95 | 地域居住支援体制強化推進 加算（算定回数） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:096 | provider:J611:02 | 96 | 地域協働加算 | when form 1701 records the item | missing | DailyRecord.RegionalCollaborationApplied / DailyRecordView / migration | provider-r7-10 p.72 |
| provider:J611:02:097 | provider:J611:02 | 97 | 有資格者支援加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:098 | provider:J611:02 | 98 | 通院支援加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:099 | provider:J611:02 | 99 | 入浴支援加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:100 | provider:J611:02 | 100 | 喀痰吸引等実施加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:101 | provider:J611:02 | 101 | 専門的支援加算（支援実施 時） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:102 | provider:J611:02 | 102 | 通所自立支援加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:103 | provider:J611:02 | 103 | 子育てサポート加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:104 | provider:J611:02 | 104 | 訪問支援員特別加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:105 | provider:J611:02 | 105 | 多職種連携支援加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:106 | provider:J611:02 | 106 | 強度行動障害児支援加算 （支援実施時） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:107 | provider:J611:02 | 107 | 集中的支援加算 | when form 1701 records the item | missing | DailyRecord.IntensiveSupportApplied / DailyRecordView / migration | provider-r7-10 p.72 |
| provider:J611:02:108 | provider:J611:02 | 108 | 緊急時受入加算 | when form 1701 records the item | missing | DailyRecord.EmergencyAdmissionApplied / DailyRecordView / migration | provider-r7-10 p.72 |
| provider:J611:02:109 | provider:J611:02 | 109 | 退居後支援 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:110 | provider:J611:02 | 110 | 自立生活支援加算（Ⅰ） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:111 | provider:J611:02 | 111 | 延長支援加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:112 | provider:J611:02 | 112 | 要支援児童加算（Ⅱ） | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |
| provider:J611:02:113 | provider:J611:02 | 113 | 自立サポート加算 | must be empty for form 1701 | generated | emit an empty CSV item because the official form 1701 matrix does not use this field | provider-r7-10 p.72 |

## 3帳票全項目

| fieldId | artifact | section | position | 公式項目名 | status | 入力源・生成規則 | CSV相互参照 | source |
| --- | --- | --- | ---: | --- | --- | --- | --- | --- |
| report:service-performance:header:001 | service-performance | header | 1 | サービス提供年月 | explicitInput | ServiceProvisionMonth | provider:J611:01:003 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:header:002 | service-performance | header | 2 | 受給者証番号 | existing | Certificate.CertificateNumber | provider:J611:01:006 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:header:003 | service-performance | header | 3 | 支給決定障害者氏名 | existing | Recipient.KanjiName |  | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:header:004 | service-performance | header | 4 | 事業所番号 | existing | Office.OfficeNumber | provider:J611:01:005 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:header:005 | service-performance | header | 5 | 事業者及びその事業所 | existing | Office.Name |  | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:header:006 | service-performance | header | 6 | 契約支給量 | existing | ContractedProvider.ContractedSupplyDays |  | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:001 | service-performance | daily | 1 | 日付 | existing | DailyRecord.ServiceDate | provider:J611:02:009 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:002 | service-performance | daily | 2 | 曜日 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model |  | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:003 | service-performance | daily | 3 | サービス提供の状況 | existing | DailyRecord.Attendance | provider:J611:02:036 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:004 | service-performance | daily | 4 | 開始時間 | missing | DailyRecord.ServiceStartTime / DailyRecordView / migration | provider:J611:02:014 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:005 | service-performance | daily | 5 | 終了時間 | missing | DailyRecord.ServiceEndTime / DailyRecordView / migration | provider:J611:02:015 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:006 | service-performance | daily | 6 | 送迎加算 往 | existing | DailyRecord.Transport | provider:J611:02:021 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:007 | service-performance | daily | 7 | 送迎加算 復 | existing | DailyRecord.Transport | provider:J611:02:022 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:008 | service-performance | daily | 8 | 訪問支援特別加算 時間数 | missing | DailyRecord.SpecialVisitSupportMinutes / DailyRecordView / migration | provider:J611:02:027 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:009 | service-performance | daily | 9 | 食事提供体制加算 | existing | DailyRecord.MealProvided | provider:J611:02:032 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:010 | service-performance | daily | 10 | 医療連携体制加算 | missing | DailyRecord.MedicalCoordinationType / DailyRecordView / migration | provider:J611:02:074 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:011 | service-performance | daily | 11 | 体験利用支援加算 | missing | DailyRecord.TrialUseSupportType / DailyRecordView / migration | provider:J611:02:083 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:012 | service-performance | daily | 12 | 地域協働加算 | missing | DailyRecord.RegionalCollaborationApplied / DailyRecordView / migration | provider:J611:02:096 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:013 | service-performance | daily | 13 | 緊急時受入加算 | missing | DailyRecord.EmergencyAdmissionApplied / DailyRecordView / migration | provider:J611:02:108 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:014 | service-performance | daily | 14 | 集中的支援加算 | missing | DailyRecord.IntensiveSupportApplied / DailyRecordView / migration | provider:J611:02:107 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:015 | service-performance | daily | 15 | 施設外支援 | missing | DailyRecord.OffsiteSupportApplied / DailyRecordView / migration | provider:J611:02:029 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:016 | service-performance | daily | 16 | 利用者確認欄 | missing | DailyRecord.RecipientConfirmation / DailyRecordView / migration |  | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:daily:017 | service-performance | daily | 17 | 備考 | existing | DailyRecord.Note | provider:J611:02:035 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:001 | service-performance | totals | 1 | サービス提供日数 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model |  | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:002 | service-performance | totals | 2 | 送迎加算 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:034 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:003 | service-performance | totals | 3 | 訪問支援特別加算 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:052 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:004 | service-performance | totals | 4 | 食事提供体制加算 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:045 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:005 | service-performance | totals | 5 | 医療連携体制加算 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:117 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:006 | service-performance | totals | 6 | 体験利用支援加算 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:125 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:007 | service-performance | totals | 7 | 地域協働加算 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:152 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:008 | service-performance | totals | 8 | 緊急時受入加算 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:168 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:009 | service-performance | totals | 9 | 集中的支援加算 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:167 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:010 | service-performance | totals | 10 | 施設外支援 当月 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:053 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:011 | service-performance | totals | 11 | 施設外支援 累計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:054 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:totals:012 | service-performance | totals | 12 | 施設外支援 上限 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model |  | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:initial-addition:001 | service-performance | initial-addition | 1 | 利用開始日 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:070 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:initial-addition:002 | service-performance | initial-addition | 2 | 30日目 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:071 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:initial-addition:003 | service-performance | initial-addition | 3 | 当月算定日数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J611:01:072 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:intensive-support:001 | service-performance | intensive-support | 1 | 支援開始日 | missing | IntensiveSupportEpisode.StartDate / DailyRecordView / migration | provider:J611:01:156 | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:pagination:001 | service-performance | pagination | 1 | 枚中 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model |  | service-performance-examples-r6-04-pdf p.12 |
| report:service-performance:pagination:002 | service-performance | pagination | 2 | 枚目 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model |  | service-performance-examples-r6-04-pdf p.12 |
| report:benefit-claim-form:header:001 | benefit-claim-form | header | 1 | 請求年月日 | explicitInput | ClaimDate |  | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:header:002 | benefit-claim-form | header | 2 | 請求先 | existing | Certificate.Municipality |  | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:header:003 | benefit-claim-form | header | 3 | 指定事業所番号 | existing | Office.OfficeNumber | provider:J111:01:005 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:header:004 | benefit-claim-form | header | 4 | 郵便番号 | missing | Office.PostalCode / OfficeView / migration |  | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:header:005 | benefit-claim-form | header | 5 | 住所（所在地） | missing | Office.Address / OfficeView / migration |  | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:header:006 | benefit-claim-form | header | 6 | 電話番号 | missing | Office.PhoneNumber / OfficeView / migration |  | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:header:007 | benefit-claim-form | header | 7 | 名称 | existing | Office.Name |  | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:header:008 | benefit-claim-form | header | 8 | 職・氏名 | missing | Office.RepresentativeTitleAndName / OfficeView / migration |  | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:header:009 | benefit-claim-form | header | 9 | サービス提供年月 | explicitInput | ServiceProvisionMonth | provider:J111:01:003 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:header:010 | benefit-claim-form | header | 10 | 請求金額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:01:006 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:claim-lines:001 | benefit-claim-form | claim-lines | 1 | 区分 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:02:006 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:claim-lines:002 | benefit-claim-form | claim-lines | 2 | 件数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:02:008 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:claim-lines:003 | benefit-claim-form | claim-lines | 3 | 単位数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:02:009 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:claim-lines:004 | benefit-claim-form | claim-lines | 4 | 費用合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:02:010 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:claim-lines:005 | benefit-claim-form | claim-lines | 5 | 給付費請求額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:02:011 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:claim-lines:006 | benefit-claim-form | claim-lines | 6 | 利用者負担額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:02:013 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:claim-lines:007 | benefit-claim-form | claim-lines | 7 | 自治体助成額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:02:014 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:totals:001 | benefit-claim-form | totals | 1 | 区分 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model |  | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:totals:002 | benefit-claim-form | totals | 2 | 件数 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:01:017 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:totals:003 | benefit-claim-form | totals | 3 | 単位数 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:01:018 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:totals:004 | benefit-claim-form | totals | 4 | 費用合計 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:01:019 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:totals:005 | benefit-claim-form | totals | 5 | 給付費請求額 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:01:020 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:totals:006 | benefit-claim-form | totals | 6 | 利用者負担額 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:01:022 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-form:totals:007 | benefit-claim-form | totals | 7 | 自治体助成額 合計 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J111:01:023 | claim-examples-r1-10-pdf p.1 |
| report:benefit-claim-detail:header:001 | benefit-claim-detail | header | 1 | 市町村番号 | missing | Certificate.MunicipalityNumber / CertificateView / migration | provider:J121:01:004 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:002 | benefit-claim-detail | header | 2 | サービス提供年月 | explicitInput | ServiceProvisionMonth | provider:J121:01:003 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:003 | benefit-claim-detail | header | 3 | 助成自治体番号 | missing | Certificate.SubsidyMunicipalityNumber / CertificateView / migration | provider:J121:01:007 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:004 | benefit-claim-detail | header | 4 | 指定事業所番号 | existing | Office.OfficeNumber | provider:J121:01:005 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:005 | benefit-claim-detail | header | 5 | 事業者及びその事業所の名称 | existing | Office.Name |  | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:006 | benefit-claim-detail | header | 6 | 受給者証番号 | existing | Certificate.CertificateNumber | provider:J121:01:006 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:007 | benefit-claim-detail | header | 7 | 支給決定障害者等氏名 | existing | Recipient.KanjiName |  | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:008 | benefit-claim-detail | header | 8 | 障害児氏名 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model |  | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:009 | benefit-claim-detail | header | 9 | 地域区分 | existing | Office.RegionGrade | provider:J121:01:010 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:010 | benefit-claim-detail | header | 10 | 就労継続支援A型事業者負担減免措置実施 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:01:011 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:011 | benefit-claim-detail | header | 11 | 利用者負担上限月額 | existing | Certificate.MonthlyCostCap | provider:J121:01:012 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:header:012 | benefit-claim-detail | header | 12 | 就労継続支援A型減免対象者 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:01:013 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:upper-limit-management:001 | benefit-claim-detail | upper-limit-management | 1 | 管理事業所 指定事業所番号 | missing | Certificate.UpperLimitManagementProviderNumber / CertificateView / migration | provider:J121:01:015 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:upper-limit-management:002 | benefit-claim-detail | upper-limit-management | 2 | 管理事業所 事業所名称 | existing | Certificate.UpperLimitManagementProvider |  | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:upper-limit-management:003 | benefit-claim-detail | upper-limit-management | 3 | 管理結果 | missing | ClaimInput.UpperLimitManagementResult / ClaimInputView / migration | provider:J121:01:016 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:upper-limit-management:004 | benefit-claim-detail | upper-limit-management | 4 | 管理結果額 | missing | ClaimInput.UpperLimitManagedAmountYen / ClaimInputView / migration | provider:J121:01:017 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:service-period:001 | benefit-claim-detail | service-period | 1 | サービス種別 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:02:007 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:service-period:002 | benefit-claim-detail | service-period | 2 | 開始年月日 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:02:008 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:service-period:003 | benefit-claim-detail | service-period | 3 | 終了年月日 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:02:009 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:service-period:004 | benefit-claim-detail | service-period | 4 | 利用日数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:02:010 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:service-period:005 | benefit-claim-detail | service-period | 5 | 入院日数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:02:011 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:claim-lines:001 | benefit-claim-detail | claim-lines | 1 | サービス内容 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model |  | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:claim-lines:002 | benefit-claim-detail | claim-lines | 2 | サービスコード | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:03:007 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:claim-lines:003 | benefit-claim-detail | claim-lines | 3 | 単位数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:03:008 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:claim-lines:004 | benefit-claim-detail | claim-lines | 4 | 回数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:03:009 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:claim-lines:005 | benefit-claim-detail | claim-lines | 5 | サービス単位数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:03:010 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:claim-lines:006 | benefit-claim-detail | claim-lines | 6 | 摘要 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:03:011 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:001 | benefit-claim-detail | summary | 1 | サービス種類コード | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:007 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:002 | benefit-claim-detail | summary | 2 | サービス利用日数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:009 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:003 | benefit-claim-detail | summary | 3 | 給付単位数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:010 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:004 | benefit-claim-detail | summary | 4 | 単位数単価 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:011 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:005 | benefit-claim-detail | summary | 5 | 総費用額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:013 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:006 | benefit-claim-detail | summary | 6 | 1割相当額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:014 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:007 | benefit-claim-detail | summary | 7 | 利用者負担額2 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:015 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:008 | benefit-claim-detail | summary | 8 | 上限月額調整 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:016 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:009 | benefit-claim-detail | summary | 9 | A型減免 事業者減免額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:017 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:010 | benefit-claim-detail | summary | 10 | A型減免 減免後利用者負担額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:018 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:011 | benefit-claim-detail | summary | 11 | 調整後利用者負担額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:019 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:012 | benefit-claim-detail | summary | 12 | 上限額管理後利用者負担額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:020 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:013 | benefit-claim-detail | summary | 13 | 決定利用者負担額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:021 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:014 | benefit-claim-detail | summary | 14 | 給付費請求額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:022 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:summary:015 | benefit-claim-detail | summary | 15 | 自治体助成分請求額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:025 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:special-benefit:001 | benefit-claim-detail | special-benefit | 1 | 算定日額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:026 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:special-benefit:002 | benefit-claim-detail | special-benefit | 2 | 日数 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:027 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:special-benefit:003 | benefit-claim-detail | special-benefit | 3 | 給付費請求額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:028 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:special-benefit:004 | benefit-claim-detail | special-benefit | 4 | 実費算定額 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model | provider:J121:04:029 | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:pagination:001 | benefit-claim-detail | pagination | 1 | 枚中 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model |  | claim-examples-r1-10-pdf p.2 |
| report:benefit-claim-detail:pagination:002 | benefit-claim-detail | pagination | 2 | 枚目 | generated | render the value deterministically from the selected claim context, official master, and calculated artifact model |  | claim-examples-r1-10-pdf p.2 |

## Phase 3-1へ移送するmissing一覧

次の項目は既存モデルから安全に得られない。Phase 3-1 AC3-8でモデル、migration、UI入力を追加するまで、備考・自由記述・近似値から推定しない。

| 対象 | fieldId | target | UI | requiredCondition |
| --- | --- | --- | --- | --- |
| CSV | provider:J111:01:004 | Certificate.MunicipalityNumber | CertificateView | always |
| CSV | provider:J111:02:004 | Certificate.MunicipalityNumber | CertificateView | always |
| CSV | provider:J121:01:004 | Certificate.MunicipalityNumber | CertificateView | always |
| CSV | provider:J121:01:007 | Certificate.SubsidyMunicipalityNumber | CertificateView | when the official J121 condition applies |
| CSV | provider:J121:01:015 | Certificate.UpperLimitManagementProviderNumber | CertificateView | when the official J121 condition applies |
| CSV | provider:J121:01:016 | ClaimInput.UpperLimitManagementResult | ClaimInputView | when the official J121 condition applies |
| CSV | provider:J121:01:017 | ClaimInput.UpperLimitManagedAmountYen | ClaimInputView | when the official J121 condition applies |
| CSV | provider:J121:02:004 | Certificate.MunicipalityNumber | CertificateView | always |
| CSV | provider:J121:03:004 | Certificate.MunicipalityNumber | CertificateView | always |
| CSV | provider:J121:04:004 | Certificate.MunicipalityNumber | CertificateView | always |
| CSV | provider:J121:05:004 | Certificate.MunicipalityNumber | CertificateView | always |
| CSV | provider:J121:05:011 | ContractedProvider.CertificateEntryNumber | CertificateView | always |
| CSV | provider:J611:01:004 | Certificate.MunicipalityNumber | CertificateView | always for form 1701 |
| CSV | provider:J611:01:156 | IntensiveSupportEpisode.StartDate | DailyRecordView | when form 1701 records the item |
| CSV | provider:J611:02:004 | Certificate.MunicipalityNumber | CertificateView | always for form 1701 |
| CSV | provider:J611:02:014 | DailyRecord.ServiceStartTime | DailyRecordView | when form 1701 records the item |
| CSV | provider:J611:02:015 | DailyRecord.ServiceEndTime | DailyRecordView | when form 1701 records the item |
| CSV | provider:J611:02:027 | DailyRecord.SpecialVisitSupportMinutes | DailyRecordView | when form 1701 records the item |
| CSV | provider:J611:02:028 | DailyRecord.SpecialVisitSupportMinutes | DailyRecordView | when form 1701 records the item |
| CSV | provider:J611:02:029 | DailyRecord.OffsiteSupportApplied | DailyRecordView | when form 1701 records the item |
| CSV | provider:J611:02:074 | DailyRecord.MedicalCoordinationType | DailyRecordView | when form 1701 records the item |
| CSV | provider:J611:02:083 | DailyRecord.TrialUseSupportType | DailyRecordView | when form 1701 records the item |
| CSV | provider:J611:02:096 | DailyRecord.RegionalCollaborationApplied | DailyRecordView | when form 1701 records the item |
| CSV | provider:J611:02:107 | DailyRecord.IntensiveSupportApplied | DailyRecordView | when form 1701 records the item |
| CSV | provider:J611:02:108 | DailyRecord.EmergencyAdmissionApplied | DailyRecordView | when form 1701 records the item |
| 帳票 | report:service-performance:daily:004 | DailyRecord.ServiceStartTime | DailyRecordView | when the official form condition applies |
| 帳票 | report:service-performance:daily:005 | DailyRecord.ServiceEndTime | DailyRecordView | when the official form condition applies |
| 帳票 | report:service-performance:daily:008 | DailyRecord.SpecialVisitSupportMinutes | DailyRecordView | when the official form condition applies |
| 帳票 | report:service-performance:daily:010 | DailyRecord.MedicalCoordinationType | DailyRecordView | when the official form condition applies |
| 帳票 | report:service-performance:daily:011 | DailyRecord.TrialUseSupportType | DailyRecordView | when the official form condition applies |
| 帳票 | report:service-performance:daily:012 | DailyRecord.RegionalCollaborationApplied | DailyRecordView | when the official form condition applies |
| 帳票 | report:service-performance:daily:013 | DailyRecord.EmergencyAdmissionApplied | DailyRecordView | when the official form condition applies |
| 帳票 | report:service-performance:daily:014 | DailyRecord.IntensiveSupportApplied | DailyRecordView | when the official form condition applies |
| 帳票 | report:service-performance:daily:015 | DailyRecord.OffsiteSupportApplied | DailyRecordView | when the official form condition applies |
| 帳票 | report:service-performance:daily:016 | DailyRecord.RecipientConfirmation | DailyRecordView | when the official form condition applies |
| 帳票 | report:service-performance:intensive-support:001 | IntensiveSupportEpisode.StartDate | DailyRecordView | when the official form condition applies |
| 帳票 | report:benefit-claim-form:header:004 | Office.PostalCode | OfficeView | when the official form condition applies |
| 帳票 | report:benefit-claim-form:header:005 | Office.Address | OfficeView | when the official form condition applies |
| 帳票 | report:benefit-claim-form:header:006 | Office.PhoneNumber | OfficeView | when the official form condition applies |
| 帳票 | report:benefit-claim-form:header:008 | Office.RepresentativeTitleAndName | OfficeView | when the official form condition applies |
| 帳票 | report:benefit-claim-detail:header:001 | Certificate.MunicipalityNumber | CertificateView | when the official form condition applies |
| 帳票 | report:benefit-claim-detail:header:003 | Certificate.SubsidyMunicipalityNumber | CertificateView | when the official form condition applies |
| 帳票 | report:benefit-claim-detail:upper-limit-management:001 | Certificate.UpperLimitManagementProviderNumber | CertificateView | when the official form condition applies |
| 帳票 | report:benefit-claim-detail:upper-limit-management:003 | ClaimInput.UpperLimitManagementResult | ClaimInputView | when the official form condition applies |
| 帳票 | report:benefit-claim-detail:upper-limit-management:004 | ClaimInput.UpperLimitManagedAmountYen | ClaimInputView | when the official form condition applies |

## 明示入力

- `ProcessingMonth`: 共通外側コントロールレコードの処理対象年月。
- `ServiceProvisionMonth`: J111/J121/J611のサービス提供年月。
- `ClaimDate`: 法定請求書の請求年月日。

処理対象年月とサービス提供年月は別入力であり、翌月や同月を自動推定しない。
