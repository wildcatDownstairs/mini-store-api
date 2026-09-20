# 实际验收结果（2026-09-18）

- PostgreSQL Server / psql：18.6 Homebrew；本机 socket `/tmp`，端口 5432，角色 aminoas。
- 数据库 ecommerce_lab；8 个业务 schema、23 张表、3 个 View。
- 最终 medium：2,591,515 行；`pg_size_pretty(pg_database_size('ecommerce_lab'))` = **605 MB**。大小包含系统目录、索引及运行后的空间占用，会随数据库活动变化。
- 84 个索引 = 32 个显式索引 + 52 个主键/唯一约束索引，完整定义见 [indexes.md](indexes.md)。
- 82 项完整性审计，异常数全部 0；包括逐项 FK、负金额、库存历史与最终余额、订单金额汇总、优惠券、退款、物流、评论和状态链。原始结果见 [verification_results.txt](verification_results.txt)。
- 15 项错误写入拒绝 / 函数正向测试通过；全部测试数据写入回滚。IDENTITY 序列在回滚后出现间隔属于正常行为。
- small seed 与安全 reset 实际执行成功；最终已恢复 medium。large 支持配置与批量导入路径，未实际导入；PowerShell 包装脚本未在 Windows 实测。PostgreSQL 13～17 的 UUID fallback 经代码分支提供，未在旧版本服务上实测。
- 全部 `09_sample_queries.sql` 与六条 EXPLAIN (ANALYZE, BUFFERS) 实际执行成功，原始输出见 [query_results.txt](query_results.txt)。
- 观察到客户订单使用 orders_customer_created_idx；模糊描述查询使用 Seq Scan；OFFSET 80000 读取 80,020 行，keyset 对比读取 20 行。单次耗时受缓存影响，不当作严格性能基准。
- 当前其他数据库仍为 db_demo、postgres、template0、template1；本任务未对其业务内容、schema 或角色执行写入。

## 检查中修正的问题

- 数据库备注必须使用共享对象目录函数 shobj_description 读取，已修复初始化识别。
- 状态流转 CHECK 的 NULL 三值逻辑可能放过非法起始状态，已用 COALESCE(..., false) 封堵并写回归检查。
- 预占函数增加订单项数量上限，拒绝重复预占。
- 抽样发现音频商品不应拥有存储容量属性，已按商品类型分配属性和价格区间，并重新生成数据。
- 超过 14 天的旧订单不再长期滞留未完成状态，近期订单保留处理中的分布。
- 示例累计月销售 SQL 使用显式 AS 避免关键字别名语法问题；已完整重跑。

最终业务数据没有残留验证异常。完整每表行数及各 5 条 customer/product/order/order_item/payment 样本见 [seed_report.md](seed_report.md)。
