export type Pagination<T> = {
    pageIdex: number;
    pageSize: number;
    count: number;
    data: T[]
}